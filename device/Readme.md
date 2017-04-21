# 組込み機器側の DeviceTwin サンプル 
**Under Construction**  
とりあえずの説明。  
1. http://github.com/Azure/azure-iot-sdk-c をクローン  
2. iothub_client/samplesに、iothub_client_sample_mqtt_egtwinをコピー。  
3. iothub_client_sample_mqtt_egtwin.cのconnectionStringに、Azure IoT Hubに登録したDeviceIdの接続文字列をセット  
4. samples/CMakeLists.txtに、コピーしたフォルダーもビルド対象になるよう記述を追加  
5. SDKの説明に従い、cmakeディレクトリを作成し、CMake実行＆ビルド  
現状は、Windowsのみテスト完了  

## 今後の予定 
- センサー情報（疑似）をDesiredPropertiesで指定されたテレメタリー間隔でIoT Hubに送信するよう変更 
- 一定時間たつと、Reported Propertiesとして送っている、実際の位置やバッテリーレベル、状態が変わる様に変更 
- Linux、MBEDでの動作確認 
- 本Readme.mdの修正 
