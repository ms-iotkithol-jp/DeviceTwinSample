# サーバーサイドの Device Twin サンプル 
ASP.NETで作成したWebアプリを改造していきます。以下の手順に従って実装していけば、IoT Hubへのデバイス登録削除、DesiredPropertiesの設定・更新が可能なWebアプリが作成できます。 
出来上がったWebアプリは、AzureのWeb Appsにデプロイ可能です。 

※ DesiredProperties - クラウド側から設定更新可能なデバイスのプロパティ群。ETag、Reported Propertiesへの対応は各自挑戦してみてね 

------------------------ 
## 必要環境 
Azure Subscription 契約が必要です。 
Visual Studio 2015 以上が必要です。
Visual Studio には、Azure SDKのインストールも行ってください。 

------------------------ 
## 事前準備 
Azure IoT Hubを、Azure管理ポータルで作成します。 
デバイスに紐づけてかつ、同期したい変数の名前と、型を決めてください。変数名はC#のプログラミングで使える文字列、利用可能な型は、string、numeric です。

------------------------ 
## 手順 
### 1. ASP.NET Web プロジェクトの作成 
Visual Studioを起動し、メニューの”ファイル”→”新規作成”→”プロジェクト”を選択します。  
新しいプロジェクトダイアログで、テンプレートの”Visual C#”→”クラウド”を選択し、”ASP.NET Web Application(.NET Framework)”を選択し、”名前”を  
**DevMgmtWebApp**  
と入力し、右下の”OK"ボタンをクリックします。 
”New ASP.NET Web Application”ダイアログで、”MVC"を選択し、”Web API”に✔をし、右下の”OK”ボタンをクリックし、プロジェクトを作成します。  
プロジェクトが作成されたら、ソリューションエクスプローラーで、”DevMgmtWebApp”プロジェクトを右クリックし、コンテキストメニューの”プロパティ”を選択します。  
”対象のフレームワーク”を、”.NET Framework 4.6”以上の最新バージョンに変え、メニューの”ビルド”→”ソリューションのリビルド”を選択し、最新バージョンの.NET Frameworkに移行します。  

### 2. デバイス管理変数を管理するモデルクラスの作成  
Azure IoT Hubに接続するデバイスに付与する管理パラメータ用のモデルクラスを作成します。  
ソリューションエクスプローラーで、”DevMgmtWebApp”プロジェクトの”Models”フォルダーを右クリックし、コンテキストメニューの、”追加”→”クラス”を選択し、  
**DeviceModels**  
という名前で、クラスを作成します。  
ここでは、管理用のパラメータとして、以下のパラメータ群を設定することとします。  
- DeviceType - デバイスの種別を保持します。文字列です。  
- TelemetryCycle - デバイスのセンサーが計測した値をIoT Hubに送信する間隔です。数字と単位（msec, sec, etc)を組み合わせた文字列とします。 
- Latitude - そのデバイスが設置される予定の位置の緯度。倍精度です。 
- Longitude - そのデバイスが設置される予定の位置の経度。倍精度です。 

※ これらのパラメータは、Azure IoT Hubが個々のデバイスに紐づけて管理するプロパティセットの、Desired Propertiesに相当します。バッテリー残量の様なデバイス由来の管理パラメータ（クラウド側で設定しても意味がない管理パラメータ）は、本サンプルでは扱いません。  

作成されたクラス、”DeviceModels”の中身に、以下の様にプロパティを追加します。 
```cs
    public class DeviceModels
    {
        public string Id { get; set; }
        public string DeviceType { get; set; }
        public string TelemetryCycle { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
```
Idは、Azure IoT Hubにデバイスを登録する際に必要な、個々のデバイスを識別する識別子、DeviceIdとして利用されます。どんなパラメータセットを定義する場合にも、必ず宣言してください。  
コーディングが終わったら、リビルドしてください。  

### 3. View付きのMVCコントローラの作成 
次に、前ステップで作成したモデルのオブジェクトを、生成、閲覧、編集、削除、一覧表示を行うための、Viewとコントローラを、Visual Studioのスキャフォールディング機能を使って生成します。  
ソリューションエクスプローラーで、DevMgmtWebAppプロジェクトの”Controllers”フォルダーを右クリックし、コンテキストメニューの、”追加”→”コントローラ”を選択し、表示されたダイアログで、”Entity Frameworkを使用した、ビューがあるMVC 5コントローラー”を選択し、”追加”をクリックします。  
表示されたダイアログで、以下の様に設定します。  
- モデルクラス - DeviceModelsを選択 
- データコンテキストクラス - 右の”＋”ボタンをクリックしデフォルトのコンテキスト型名で追加します 
- ビュー - ”ビューの生成”、”スクリプトライブラリの参照”、”レイアウトページの使用”全てに✔を入れます 
- コントローラー名 - 表示されたままの文字列をそのまま利用します 

設定が終わったら、”追加”ボタンをクリックします。この結果、Controllersフォルダーの下に、”DeviceModelsController.cs”、Modelsフォルダーの下に、”DevMgmtWebAppContext.cs”、Viewsフォルダーの下に”DeviceModels”という名前のフォルダーが作成され、その下に、”Create.cshtml”、”Delete.cshtml”、”Details.cshtml”、”Edit.cshtml”というファイル群が作成されます。  


### 4. Azure IoT Hubにアクセスする為のNuGetライブラリの追加  
次に、Azure IoT HubにアクセスするためのSDKライブラリを組み込みます。ソリューションエクスプローラーで、DevMgmtWebAppプロジェクトの”参照”フォルダーを右クリックし、コンテキストメニューの”NuGetパッケージの管理”を選択します。  
NuGetのViewが表示されたら、”参照”を選択して、検索窓に”Azure Devices”と入力します。  
結果として表示される一覧の中から、”Microsoft.Azure.Devices”を選択し、右側の”インストール”ボタンをクリックします。変更の確認、ライセンスへの同意にそれぞれ、”OK”、”同意する”をクリックしインストールします。  
※ 検索の際、”Microsoft.Azure.Devices.Client”等、Devices.の後ろに文字列が続く候補が多数現れるので、選択の際は気を付けてくださいね。  
インストールが完了したら、リビルドを実行してください。  

### 5. Azure IoT Hubと同期する為のライブラリの組込み  
次に、Azure IoT HubとASP.NET MVC Web Appを紐づけるクラスを組み込みます。 
このリポジトリの、[Models/IoTHubContext.cs](./Models/IoTHubContext.cs)をDevMgmtWebAppプロジェクトのModelsフォルダーの下に追加します。このリポジトリをクローンしている場合には、該当するファイルが格納されたフォルダーをファイルエクスプローラで表示し、IoTHubContext.csをマウスで、Visual StudioのソリューションエクスプローラーのModelsフォルダーにドラッグ＆ドロップすることにより、追加可能です。  
この作業を実行後、2番目のステップで作成した、Modelsフォルダーの下のDeviceModels.csを再度エディターで開き、以下の様に、IoTHubContext.cs内で定義されたIModelDeviceを実装するように修正を加えます。  
```cs
    public class DeviceModels : IModeledDevice
    {
        public string Id { get; set; }
        ...
```
すると、IModelDeviceの下に赤波線が表示されるので、赤波線の付近にマウスを近づけ、表示された電球マークの小さな黒▼をクリックして、IModelDeviceが規定するメソッドの実装を選択し、IModelDeviceが実装を要求する、DesiredPropertiesToJsonとSetDesiredPropertiesという2つのメソッドを実装します。結果として、DeviceModelsクラスは次のようなコードになります。
```cs
    public class DeviceModels : IModeledDevice
    {
        public string Id { get; set; }
        public string DeviceType { get; set; }
        public string TelemetryCycle { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public string DesiredPropertiesToJson()
        {
            throw new NotImplementedException();
        }

        public void SetDesiredProperties(string json)
        {
            throw new NotImplementedException();
        }
    }
```
追加された二つのメソッドは、Azure IoT HubにDesiredPropertiesとして書き込む際のパラメータ設定をJSON化するメソッドと、DesiredPropertiesから読み出したJSONからパラメータ設定を行う役割を果たします。独自の管理パラメータを追加する場合は、このサンプルを参考に、この二つのメソッドの実装をパラメータセットに合わせて書き換えが必要です。  
※ 作業の簡略化の為、リポジトリのModelsフォルダーにはそれぞれのコードフラグメントを用意しているので、手書きコーディングに自信が無い方は、そちらを利用してくださいね。  

### 6. 同期ライブラリへの接続文字列設定  
前ステップで追加した、IoTHubContext.csに、Azure IoT Hubに接続するための接続文字列を設定します。  
[Azureポータル](http://portal.azure.com)で作成済みのAzure IoT Hubの共有アクセスポリシーのiothubownerの接続文字列を確認し、
```cs
    public class DeviceModelsIoTHubContext : IoTHubContext<DeviceModels>
    {
        static string cs = "<< IoT Hub Connection String >>";

        public DeviceModelsIoTHubContext() : base(connectionString: cs)
        {
            DeviceModels = new IoTHubDeviceSet<DeviceModels>(registryManager);
```
DeviceModelsIoTHubContextクラスの、csの<< IoT Hub Connection String >>を接続文字列で置き換えます。置き換え後は、
```cs
        static string cs = "HostName=…;SharedAccessKeyName=iothubowner;SharedAccessKey=...";
```
の様な文字列になっているはずです。これで、Azure IoT Hubに接続する為の情報設定は完了です。このセキュリティ情報を使って、Azure IoT HubのRegistry機能にアクセスします。

### 7. スキャフォールディングで生成したコントローラの修正 
Step 3で自動生成されたDeviceModelsController.csの実装は、SQL Databaseにパラメータ群を保持するコードになっています。これを、Step 5、6で追加したIoTHubContext.csを使ってAzure IoT Hubにパラメータ群を保持するよう、変更を加えていきます。先ずは、DeviceModelsController.csをエディターで開きます。  
先ずは、DeviceModelsControllerクラスの冒頭の部分に修正を加えます。自動生成された修正前のコードは、
```cs
namespace DevMgmtWebApp.Controllers
{
    public class DeviceModelsController : Controller
    {
        private DevMgmtWebAppContext db = new DevMgmtWebAppContext();

```
となっています。これを、以下の様に修正します。
```cs
    public class DeviceModelsController : AsyncController
    {
        private DeviceModelsIoTHubContext db = new DeviceModelsIoTHubContext();
```
DevieModelsIoTHubContextクラスが提供するメソッドは非同期メソッドなので、それを使えるように、親クラスをAsyncControllerに変更し、db変数をDeviceModelsIoTHubContextのインスタンスに変更しています。  
次に、非同期メソッドに対応する為、DeviceModelsControllerクラスの、CreateとDisposeの2つのメソッド以外の全てのメソッドの戻り値の型を”async Task<ActionResult>”に変更します。例えば、
```cs
        public ActionResult Index()
        {
            return View(db.DeviceModels.ToList());
        }
```
の場合は、
```cs
        public async Task<ActionResult> Index()
```
の様に修正します。Taskは、Using宣言が足りないので、ファイルのUsing宣言をしている個所に、
```cs
using System.Threading.Tasks;
```
を追加してください。  
次に、DeviceModelsIoTHubContextクラスのメソッドは非同期メソッドなので、Controllerクラスのメソッドの中で、このクラスのメソッドをコールしているところは、全て”await”を付与していきます。例えば、Indexメソッドの場合は、以下の様な形式に変えます。
```cs
        public async Task<ActionResult> Index()
        {
            return View(await db.DeviceModels.ToList());
        }
```
Visual Studioのエディタ上で、緑や赤の波線が下に表示されているものは大抵修正対象です。最終的には、以下の様なコードが出来上がります。
```cs
    public class DeviceModelsController : AsyncController
    {
        private DeviceModelsIoTHubContext db = new DeviceModelsIoTHubContext();

        // GET: DeviceModels
        public async Task<ActionResult> Index()
        {
            return View(await db.DeviceModels.ToList());
        }

        // GET: DeviceModels/Details/5
        public async Task<ActionResult> Details(string id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            DeviceModels deviceModels = await db.DeviceModels.Find(id);
            if (deviceModels == null)
            {
                return HttpNotFound();
            }
            return View(deviceModels);
        }

        // GET: DeviceModels/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: DeviceModels/Create
        // 過多ポスティング攻撃を防止するには、バインド先とする特定のプロパティを有効にしてください。
        // 詳細については、http://go.microsoft.com/fwlink/?LinkId=317598 を参照してください。
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create([Bind(Include = "Id,DeviceType,TelemetryCycle,Latitude,Longitude")] DeviceModels deviceModels)
        {
            if (ModelState.IsValid)
            {
                await db.DeviceModels.Add(deviceModels);
                await db.SaveChanges();
                return RedirectToAction("Index");
            }

            return View(deviceModels);
        }

        // GET: DeviceModels/Edit/5
        public async Task<ActionResult> Edit(string id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            DeviceModels deviceModels = await db.DeviceModels.Find(id);
            if (deviceModels == null)
            {
                return HttpNotFound();
            }
            return View(deviceModels);
        }

        // POST: DeviceModels/Edit/5
        // 過多ポスティング攻撃を防止するには、バインド先とする特定のプロパティを有効にしてください。
        // 詳細については、http://go.microsoft.com/fwlink/?LinkId=317598 を参照してください。
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit([Bind(Include = "Id,DeviceType,TelemetryCycle,Latitude,Longitude")] DeviceModels deviceModels)
        {
            if (ModelState.IsValid)
            {
                (await db.Entry(deviceModels)).State = EntityState.Modified;
                await db.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(deviceModels);
        }

        // GET: DeviceModels/Delete/5
        public async Task<ActionResult> Delete(string id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            DeviceModels deviceModels = await db.DeviceModels.Find(id);
            if (deviceModels == null)
            {
                return HttpNotFound();
            }
            return View(deviceModels);
        }

        // POST: DeviceModels/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(string id)
        {
            DeviceModels deviceModels = await db.DeviceModels.Find(id);
            await db.DeviceModels.Remove(deviceModels);
            await db.SaveChanges();
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
```
※ Editメソッドだけちょっとパターンが違うのでご注意。 
これで、DeviceModelsControllerクラスの実装を、DeviceModelsIoTHubContextに接続する為の修正は完了です。  

### 8. スキャフォールディングで生成したビューの修正  
これまでのステップで大概は修正が終わっているのですが、デバイス登録、一覧表示等で使うView（UI）をわかりやすくするために、DeviceModelsクラスで定義したIdプロパティを”DeviceId”という名前で表示するように修正を加えます。
先ずは、Views/DeviceModelsフォルダーのCreate.cshtmlファイルをエディタで表示します。
この19行目付近の、
```
           @Html.LabelFor(model => model.Id, htmlAttributes: new { @class = "control-label col-md-2" })
 ```
 の、”LabelFor(model => model.Id”を以下の様に変更します。
 ```
             @Html.Label("DeviceId", htmlAttributes: new { @class = "control-label col-md-2" })
```
次に、Index.cshtmlを開きます。デフォルトではId（DeviceId）が表示されないので、14行目、31行目にそれぞれ、Id（DeviceId）を表示するコードを加えます。  
14行目
```
<table class="table">
    <tr>
        <th>
            @Html.Label("DeviceId")
        </th>
        <th>
            @Html.DisplayNameFor(model => model.DeviceType)
```
31行目
```
@foreach (var item in Model) {
    <tr>
        <td>
            @Html.DisplayFor(modelItem =>item.Id)
        </td>
        <td>
            @Html.DisplayFor(modelItem => item.DeviceType)
```
以上で、修正は完了です。

### 9. 実行 
修正が全て終わったら、ローカル環境で先ずはテストを実施しましょう。デバッグ実行すると、ブラウザが開いて、http://localhost:xxxx/が開きます。URLをhttp://localhost:xxxx/DeviceModelsに変更すると、デバイス種別、テレメタリー間隔、緯度、経度をDevice TwinのDesiredPropertiesに格納可能なデバイス登録削除用のWeb UIが表示されます。Device ExplorerでAzure IoT Hub側でのデバイスの登録状況を確認しながら、デバイスの登録、DesiredPropertiesの編集、デバイスの削除などを試してみてください。  
このリポジトリの[client側のサンプル](../client/readme.md)をデバイス上で実行し、Azure IoT Hubに接続している場合には、こちら側のWeb UIでのパラメータ変更時、デバイス側に通知が行くので、そちらもご確認ください。

## 補足 
このサンプルでは、”DeviceModels”という名前でモデルを作成しました。よりシナリオ依存な、Trackとか、Guitarとか、FAControllerとかスペシフィックな名前に変えたい場合には、このサンプルを参考に、適宜関連するクラス名や、プロパティ名を変更して応用をお願いします。  
