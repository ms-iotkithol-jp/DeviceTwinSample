#Copyright (c) Microsoft. All rights reserved.
#Licensed under the MIT license. See LICENSE file in the project root for full license information.

#this is CMakeLists.txt for iothub_client_sample_mqtt_egtwin

macro(compileAsC99)
  if (CMAKE_VERSION VERSION_LESS "3.1")
    if (CMAKE_C_COMPILER_ID STREQUAL "GNU")
      set (CMAKE_C_FLAGS "--std=c99 ${CMAKE_C_FLAGS}")
    if (NOT IN_OPENWRT)
      set (CMAKE_CXX_FLAGS "--std=c++11 ${CMAKE_CXX_FLAGS}")
    endif()
    endif()
  else()
    set (CMAKE_C_STANDARD 99)
    set (CMAKE_CXX_STANDARD 11)
  endif()
endmacro(compileAsC99)

if(NOT ${use_mqtt})
    message(FATAL_ERROR "iothub_client_sample_mqtt_egtwin being generated without mqtt support")
endif()



compileAsC99()


set(iothub_client_sample_mqtt_egtwin_c_files
    iothub_client_sample_mqtt_egtwin.c
)

set(iothub_client_sample_mqtt_egtwin_h_files
    iothub_client_sample_mqtt_egtwin.h
)

IF(WIN32)
    #windows needs this define
    add_definitions(-D_CRT_SECURE_NO_WARNINGS)
    add_definitions(-DGB_MEASURE_MEMORY_FOR_THIS -DGB_DEBUG_ALLOC)
    add_definitions(/WX-)
ENDIF(WIN32)
set(AZUREIOT_INC_FOLDER "../../../serializer/inc" "../../../parson/inc" ".")
include_directories(${AZUREIOT_INC_FOLDER})

#include_directories(.)

add_executable(iothub_client_sample_mqtt_egtwin ${iothub_client_sample_mqtt_egtwin_c_files} ${iothub_client_sample_mqtt_egtwin_h_files})

target_link_libraries(iothub_client_sample_mqtt_egtwin  
    serializer
    iothub_client_mqtt_transport
    iothub_client 
)

