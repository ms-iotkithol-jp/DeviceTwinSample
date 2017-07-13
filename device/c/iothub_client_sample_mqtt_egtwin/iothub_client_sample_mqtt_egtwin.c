// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdbool.h>

#include "serializer.h"
#include "parson.h"

#include "iothub_client.h"
#include "iothub_message.h"
#include "azure_c_shared_utility/threadapi.h"
#include "azure_c_shared_utility/crt_abstractions.h"
#include "azure_c_shared_utility/platform.h"
#include "iothubtransportmqtt.h"

#ifdef MBED_BUILD_TIMESTAMP
#include "certs.h"
#endif // MBED_BUILD_TIMESTAMP

/*String containing Hostname, Device Id & Device Key in the format:                         */
/*  "HostName=<host_name>;DeviceId=<device_id>;SharedAccessKey=<device_key>"                */
/*  "HostName=<host_name>;DeviceId=<device_id>;SharedAccessSignature=<device_sas_token>"    */
static const char* connectionString = "<< Azure IoT Hub Connection String >>";

/**
	enumeration of machine status
*/
/* enum values are in lower case per design */
#define MACHINE_STATUS_VALUES \
        initial, \
        running, \
        broken
DEFINE_ENUM(MACHINE_STATUS, MACHINE_STATUS_VALUES)
DEFINE_ENUM_STRINGS(MACHINE_STATUS, MACHINE_STATUS_VALUES)

/**
	Definition of Device Twin Reported Properties
*/
BEGIN_NAMESPACE(EGIoTHoL);

/**
	Sample for structured properties
*/
DECLARE_STRUCT(position_t,
double, latitude,
double, longitude
);

/**
	Reported Properties will be send to IoT Hub as
	{"firmware":"...","realPosiiton":{"latitude":37.5...,"longitude":137.1...},"machineStatus":"running","batteryLevel":3.3}
*/
DECLARE_MODEL(thingie_t,
WITH_REPORTED_PROPERTY(ascii_char_ptr, firmware),
WITH_REPORTED_PROPERTY(position_t, realPosition),
WITH_REPORTED_PROPERTY(ascii_char_ptr, machineStatus),
WITH_REPORTED_PROPERTY(double, batteryLevel)
);

END_NAMESPACE(EGIoTHoL);

/**
	Desired Properties will be received from IoT Hub
*/

typedef struct EG_DESIRED_PROPERTIES_tag
{
	char* deviceType;
	position_t desiredPosition;
	int telemetryCycle;
} EG_DESIRED_PROPERTIES;

typedef struct PHYSICAL_DEVICE_TAG
{
	thingie_t   *iot_device;
	EG_DESIRED_PROPERTIES desiredProperties;
	LOCK_HANDLE  status_lock;
	MACHINE_STATUS status;
} PHYSICAL_DEVICE;

char *device_get_firmware(void)
{
	char* version = "Windows 10";
	//char* buf = (char*)malloc(sizeof(version) + 1);
	//strcpy(buf, version);
	return version;
}

static void ReportedStateCallback(int status_code, void* userContextCallback)
{
	(void)(userContextCallback);
	LogInfo("DeviceTwin CallBack: Status_code = %u", status_code);
}

static bool set_physical_device_machine_status(PHYSICAL_DEVICE *physical_device, MACHINE_STATUS newStatus)
{
	bool retValue;
	if (Lock(physical_device->status_lock) != LOCK_OK)
	{
		LogError("failed to acquire lock");
		retValue = false;
	}
	else
	{
		physical_device->status = newStatus;
		retValue = true;
		if (Unlock(physical_device->status_lock) != LOCK_OK)
		{
			LogError("failed to release lock");
		}
	}
	return retValue;
}

static MACHINE_STATUS get_physical_device_machine_status(const PHYSICAL_DEVICE *physical_device)
{
	MACHINE_STATUS retValue;

	if (Lock(physical_device->status_lock) != LOCK_OK)
	{
		LogError("failed to acquire lock");
		retValue = broken;
	}
	else
	{
		retValue = physical_device->status;
		if (Unlock(physical_device->status_lock) != LOCK_OK)
		{
			LogError("failed to release lock");
		}
	}
	return retValue;
}

static PHYSICAL_DEVICE* physical_device_new(thingie_t *iot_device)
{
	PHYSICAL_DEVICE *retValue = malloc(sizeof(PHYSICAL_DEVICE));
	if (retValue == NULL)
	{
		LogError("failed to allocate memory for physical device structure");
	}
	else
	{
		retValue->status_lock = Lock_Init();
		if (retValue->status_lock == NULL)
		{
			LogError("failed to create a lock");
			free(retValue);
			retValue = NULL;
		}
		else {
			retValue->iot_device = iot_device;
			retValue->status = initial;
		}
	}

	return retValue;
}

static void physical_device_delete(PHYSICAL_DEVICE *physical_device)
{
	if (Lock_Deinit(physical_device->status_lock) == LOCK_ERROR)
	{
		LogError("failed to release lock handle");
	}
	free(physical_device);
}

typedef struct  EG_IOTHUBCLIENT_CONTEXT_tag
{
	IOTHUB_CLIENT_HANDLE iotHubClientHandle;
	PHYSICAL_DEVICE* physicalDevice;
} EG_IOTHUBCLIENT_CONTEXT;

double realLatitude = 200;
double realLongitude = 123;
double currentBatteryLevel = 3.3;

/**
 Send Reported Properties of thingie_t to IoT Hub
*/
static bool SendReportedProperties(const PHYSICAL_DEVICE *physical_device, IOTHUB_CLIENT_HANDLE iotHubClientHandle)
{
	unsigned char *buffer;
	size_t         bufferSize;
	bool           retValue;

	thingie_t *iot_device = physical_device->iot_device;
	if (iot_device->firmware == NULL)
	{
		retValue = false;
		LogError("Failed to retrieve the firmware for device.");
		retValue = false;
	}
	else
	{
		iot_device->machineStatus = (char *)ENUM_TO_STRING(MACHINE_STATUS, get_physical_device_machine_status(physical_device));
		iot_device->realPosition.latitude = realLatitude;
		iot_device->realPosition.longitude = realLongitude;
		iot_device->batteryLevel = currentBatteryLevel;

		/*serialize the model using SERIALIZE_REPORTED_PROPERTIES */
		if (SERIALIZE_REPORTED_PROPERTIES(&buffer, &bufferSize, *iot_device) != CODEFIRST_OK)
		{
			retValue = false;
			LogError("Failed serializing reported state.");
			retValue = false;
		}
		else
		{
			/* send the data up stream*/
			IOTHUB_CLIENT_RESULT result = IoTHubClient_SendReportedState(iotHubClientHandle, buffer, bufferSize, ReportedStateCallback, NULL);
			if (result != IOTHUB_CLIENT_OK)
			{
				retValue = false;
				LogError("Failure sending data!!!");
				retValue = false;
			}
			else
			{
				ThreadAPI_Sleep(1);
				retValue = true;
			}
			free(buffer);
		}
	//	free(iot_device->iothubDM.firmwareVersion);
	}
	return retValue;
}

static int callbackCounter;
static char msgText[1024];
static char propText[1024];
static bool g_continueRunning;
#define MESSAGE_COUNT 5
#define DOWORK_LOOP_NUM     3


typedef struct EVENT_INSTANCE_TAG
{
    IOTHUB_MESSAGE_HANDLE messageHandle;
    size_t messageTrackingId;  // For tracking the messages within the user callback.
} EVENT_INSTANCE;

static IOTHUBMESSAGE_DISPOSITION_RESULT ReceiveMessageCallback(IOTHUB_MESSAGE_HANDLE message, void* userContextCallback)
{
    int* counter = (int*)userContextCallback;
    const char* buffer;
    size_t size;
    MAP_HANDLE mapProperties;
    const char* messageId;
    const char* correlationId;

    // Message properties
    if ((messageId = IoTHubMessage_GetMessageId(message)) == NULL)
    {
        messageId = "<null>";
    }

    if ((correlationId = IoTHubMessage_GetCorrelationId(message)) == NULL)
    {
        correlationId = "<null>";
    }

    // Message content
    if (IoTHubMessage_GetByteArray(message, (const unsigned char**)&buffer, &size) != IOTHUB_MESSAGE_OK)
    {
        (void)printf("unable to retrieve the message data\r\n");
    }
    else
    {
        (void)printf("Received Message [%d]\r\n Message ID: %s\r\n Correlation ID: %s\r\n Data: <<<%.*s>>> & Size=%d\r\n", *counter, messageId, correlationId, (int)size, buffer, (int)size);
        // If we receive the work 'quit' then we stop running
        if (size == (strlen("quit") * sizeof(char)) && memcmp(buffer, "quit", size) == 0)
        {
            g_continueRunning = false;
        }
    }

    // Retrieve properties from the message
    mapProperties = IoTHubMessage_Properties(message);
    if (mapProperties != NULL)
    {
        const char*const* keys;
        const char*const* values;
        size_t propertyCount = 0;
        if (Map_GetInternals(mapProperties, &keys, &values, &propertyCount) == MAP_OK)
        {
            if (propertyCount > 0)
            {
                size_t index;

                printf(" Message Properties:\r\n");
                for (index = 0; index < propertyCount; index++)
                {
                    (void)printf("\tKey: %s Value: %s\r\n", keys[index], values[index]);
                }
                (void)printf("\r\n");
            }
        }
    }

    /* Some device specific action code goes here... */
    (*counter)++;
    return IOTHUBMESSAGE_ACCEPTED;
}

/**
	Receive Device Twin Properties from IoT Hub
*/
void DeviceTwinCallback(DEVICE_TWIN_UPDATE_STATE update_state, const unsigned char* payLoad, size_t size, void* userContextCallback)
{
	EG_IOTHUBCLIENT_CONTEXT* context = (EG_IOTHUBCLIENT_CONTEXT*)userContextCallback;
	printf("DeviceTwinCallback - status=%d\n", update_state);
	char* tmpBuf = (char*)malloc(size + 1);
	memcpy(tmpBuf, payLoad, size);
	tmpBuf[size] = '\0';
	printf("palyLoad[%d]=%s\n", size, tmpBuf);
	char* buf = "{\"desired\": {\"dmConfig\": {\"DeviceType\": \"windows\",\"TelemetryCycle\" : \"10000\",\"Latitude\" : 39.1,\"Longitude\" : 138.1},\"$version\": 4	},\"reported\": {\"iothubDM\": {\"firmwareVersion\": \"Windows 10\",\"firmwareUpdate\" : {\"status\": \"downloadComplete\"}},\"batteryLevel\": 3.3,\"machineStatus\" : \"running\",\"realPosition\" : {\"latitude\": 200,\"longitude\" : 123	},\"firmware\" : \"Windows 10\",\"$version\" : 16}}";
	JSON_Value *json = json_parse_string(buf);
	JSON_Object* jsonRoot = json_value_get_object(json);
	JSON_Object* desiredJson =json_object_get_object(jsonRoot , "desired");
	
	context->physicalDevice->desiredProperties.desiredPosition.latitude = json_object_dotget_number(desiredJson, "dmConfig.Latitude");
	context->physicalDevice->desiredProperties.desiredPosition.longitude = json_object_dotget_number(desiredJson, "dmConfig.Longitude");

	JSON_Object* dmConfigJson = json_object_get_object(desiredJson, "dmConfig");
	const char* tc = json_object_get_string(dmConfigJson, "TelemetryCycle");
	sscanf(tc, "%d", &(context->physicalDevice->desiredProperties.telemetryCycle));
	const char* deviceType = json_object_get_string(dmConfigJson, "DeviceType");
	context->physicalDevice->desiredProperties.deviceType = (char*)malloc(sizeof(deviceType) + 1);
	strcpy(context->physicalDevice->desiredProperties.deviceType, deviceType);
	json_value_free(json);
}

static void SendConfirmationCallback(IOTHUB_CLIENT_CONFIRMATION_RESULT result, void* userContextCallback)
{
    EVENT_INSTANCE* eventInstance = (EVENT_INSTANCE*)userContextCallback;
    (void)printf("Confirmation[%d] received for message tracking id = %zu with result = %s\r\n", callbackCounter, eventInstance->messageTrackingId, ENUM_TO_STRING(IOTHUB_CLIENT_CONFIRMATION_RESULT, result));
    /* Some device specific action code goes here... */
    callbackCounter++;
    IoTHubMessage_Destroy(eventInstance->messageHandle);
}

void iothub_client_sample_mqtt_run(void)
{
	EG_IOTHUBCLIENT_CONTEXT* context = (EG_IOTHUBCLIENT_CONTEXT*)malloc(sizeof(EG_IOTHUBCLIENT_CONTEXT));

	thingie_t *iot_device = CREATE_MODEL_INSTANCE(EGIoTHoL, thingie_t);
	iot_device->batteryLevel = currentBatteryLevel;
	iot_device->firmware = device_get_firmware();
	iot_device->realPosition.latitude = realLatitude;
	iot_device->realPosition.longitude = realLongitude;

	context->physicalDevice = physical_device_new(iot_device);
	set_physical_device_machine_status(context->physicalDevice, initial);

	IOTHUB_CLIENT_HANDLE iotHubClientHandle;

    EVENT_INSTANCE messages[MESSAGE_COUNT];

    g_continueRunning = true;
    srand((unsigned int)time(NULL));
    double avgWindSpeed = 10.0;
    
    callbackCounter = 0;
    int receiveContext = 0;

    if (platform_init() != 0)
    {
        (void)printf("Failed to initialize the platform.\r\n");
    }
    else
    {
		set_physical_device_machine_status(context->physicalDevice, running);

		if ((iotHubClientHandle = IoTHubClient_CreateFromConnectionString(connectionString, MQTT_Protocol)) == NULL)
        {
            (void)printf("ERROR: iotHubClientHandle is NULL!\r\n");
        }
        else
        {
			context->iotHubClientHandle = iotHubClientHandle;
			IoTHubClient_SetDeviceTwinCallback(iotHubClientHandle, DeviceTwinCallback, context);

            bool traceOn = true;
            IoTHubClient_SetOption(iotHubClientHandle, "logtrace", &traceOn);

			SendReportedProperties(context->physicalDevice, iotHubClientHandle);

#ifdef MBED_BUILD_TIMESTAMP
            // For mbed add the certificate information
            if (IoTHubClient_SetOption(iotHubClientHandle, "TrustedCerts", certificates) != IOTHUB_CLIENT_OK)
            {
                printf("failure to set option \"TrustedCerts\"\r\n");
            }
#endif // MBED_BUILD_TIMESTAMP

            /* Setting Message call back, so we can receive Commands. */
            if (IoTHubClient_SetMessageCallback(iotHubClientHandle, ReceiveMessageCallback, &receiveContext) != IOTHUB_CLIENT_OK)
            {
                (void)printf("ERROR: IoTHubClient_SetMessageCallback..........FAILED!\r\n");
            }
            else
            {
                (void)printf("IoTHubClient_SetMessageCallback...successful.\r\n");

                /* Now that we are ready to receive commands, let's send some messages */
                size_t iterator = 0;
                do
                {
                    if (iterator < MESSAGE_COUNT)
                    {
                        sprintf_s(msgText, sizeof(msgText), "{\"deviceId\":\"myFirstDevice\",\"windSpeed\":%.2f}", avgWindSpeed + (rand() % 4 + 2));
                        if ((messages[iterator].messageHandle = IoTHubMessage_CreateFromByteArray((const unsigned char*)msgText, strlen(msgText))) == NULL)
                        {
                            (void)printf("ERROR: iotHubMessageHandle is NULL!\r\n");
                        }
                        else
                        {
                            messages[iterator].messageTrackingId = iterator;
                            MAP_HANDLE propMap = IoTHubMessage_Properties(messages[iterator].messageHandle);
                            (void)sprintf_s(propText, sizeof(propText), "PropMsg_%zu", iterator);
                            if (Map_AddOrUpdate(propMap, "PropName", propText) != MAP_OK)
                            {
                                (void)printf("ERROR: Map_AddOrUpdate Failed!\r\n");
                            }

                            if (IoTHubClient_SendEventAsync(iotHubClientHandle, messages[iterator].messageHandle, SendConfirmationCallback, &messages[iterator]) != IOTHUB_CLIENT_OK)
                            {
                                (void)printf("ERROR: IoTHubClient_SendEventAsync..........FAILED!\r\n");
                            }
                            else
                            {
                                (void)printf("IoTHubClient_SendEventAsync accepted message [%d] for transmission to IoT Hub.\r\n", (int)iterator);
                            }
                        }
                    }
            //        IoTHubClient_DoWork();
                    ThreadAPI_Sleep(1);

                    iterator++;
                } while (g_continueRunning);

                (void)printf("iothub_client_sample_mqtt has gotten quit message, call DoWork %d more time to complete final sending...\r\n", DOWORK_LOOP_NUM);
                size_t index = 0;
                for (index = 0; index < DOWORK_LOOP_NUM; index++)
                {
             //       IoTHubClient_DoWork(iotHubClientHandle);
                    ThreadAPI_Sleep(1);
                }
            }
            IoTHubClient_Destroy(iotHubClientHandle);
        }
        platform_deinit();
    }
}


int main(void)
{
    iothub_client_sample_mqtt_run();
    return 0;
}
