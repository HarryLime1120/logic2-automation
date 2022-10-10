/* This is an attempt to port the python example to C# in Visual Studio 2022.
 * Comments & code from the python example are kept as comments.
 * 
 * GRPC info can be found here:
 * https://docs.microsoft.com/de-de/aspnet/core/tutorials/grpc/grpc-start?view=aspnetcore-6.0&tabs=visual-studio
 * https://github.com/grpc/grpc
 * 
 * 3 nu-get packages are in use. They were installed via Packet Manager Console:
 * Install-Package Grpc.Net.Client
 * Install-Package Google.Protobuf
 * Install-Package Grpc.Tools
 * 
 * The saleae.proto file is copied from proto\saleae\grpc before every build.
 */

using Google.Protobuf.WellKnownTypes;
using Grpc.Net.Client;
using Saleae.Automation;

// The port number must match the port of the gRPC server.
const string adr = "http://localhost:10430";

// print version info
Console.WriteLine("V 0.1");

// Connect to the running Logic 2 Application on port `10430`.
// Alternatively you can use automation.Manager.launch() to launch a new Logic 2 process - see
// the API documentation for more details.
// Using the `with` statement will automatically call manager.close() when exiting the scope. If you
// want to use `automation.Manager` outside of a `with` block, you will need to call `manager.close()` manually.
// Python:    with automation.Manager.connect(port = 10430) as manager:

Console.WriteLine("Internal setup with {0}:", adr);
GrpcChannelOptions channelOptions = new GrpcChannelOptions();
channelOptions.UnsafeUseInsecureChannelCallCredentials = true;
using var channel = GrpcChannel.ForAddress(adr, channelOptions);

if (channel != null)
{
    Console.WriteLine("Contacting ... ");
    var client = new Saleae.Automation.Manager.ManagerClient(channel);

    Console.WriteLine("\nAsking for App Info");
    var appinfo = await client.GetAppInfoAsync(
        new Saleae.Automation.GetAppInfoRequest { });
    Console.WriteLine("Greetings from Saleae: " + appinfo.AppInfo);
    Console.WriteLine("Reported: {0}", appinfo);

    Console.WriteLine("\nAsking for Devices (incl SimulationDevices): ");
    var Devices = client.GetDevices(
        new Saleae.Automation.GetDevicesRequest { IncludeSimulationDevices = true });
    Console.WriteLine("Reported: {0}", Devices);

    // Configure the capturing device to record on digital channels 0, 1, 2, and 3,
    // with a sampling rate of 10 MSa/s, and a logic level of 3.3V.
    // The settings chosen here will depend on your device's capabilities and what
    // you can configure in the Logic 2 UI.
    //device_configuration = automation.LogicDeviceConfiguration(
    //    enabled_digital_channels =[0, 1, 2, 3],
    //    digital_sample_rate = 10_000_000,
    //    digital_threshold_volts = 3.3,
    //)

    Console.WriteLine("LogicDeviceConfiguration: ");
    Saleae.Automation.LogicDeviceConfiguration device_configuration = new Saleae.Automation.LogicDeviceConfiguration
    { DigitalSampleRate = 10000000, DigitalThresholdVolts = 3.3};
    Saleae.Automation.LogicChannels device_channels = new Saleae.Automation.LogicChannels();
    device_channels.DigitalChannels.Add(0);
    device_channels.DigitalChannels.Add(1);
    device_channels.DigitalChannels.Add(2);
    device_channels.DigitalChannels.Add(3);
    device_configuration.LogicChannels = device_channels;

    // if analogue inputs are needed
    // device_channels.AnalogChannels.Add(0);
    // device_configuration.AnalogSampleRate = 50000000;

    Console.WriteLine("TimedCaptureMode: ");
    //  Record 5 seconds of data before stopping the capture
    //capture_configuration = automation.CaptureConfiguration(
    //    capture_mode = automation.TimedCaptureMode(duration_seconds = 5.0)
    //)
    Saleae.Automation.CaptureConfiguration capture_configuration = new Saleae.Automation.CaptureConfiguration
        {TimedCaptureMode = new TimedCaptureMode { DurationSeconds = 5.0 } };

    // Start a capture - the capture will be automatically closed when leaving the `with` block
    // Note: We are using serial number 'F4241' here, which is the serial number for
    // the Logic Pro 16 demo device.You can remove the device_id and the first physical
    // device found will be used, or you can use your device's serial number.
    // See the "Finding the Serial Number of a Device" section for information on finding your
    // device's serial number.
    // with manager.start_capture(
    //        device_id = 'F4241',
    // device_configuration = device_configuration,
    //        capture_configuration = capture_configuration) as capture:

    Console.WriteLine("\nStart capture: ");
    var startCaptureReply = client.StartCapture(new StartCaptureRequest
    {
        DeviceId = "F4241",
        CaptureConfiguration = capture_configuration,
        LogicDeviceConfiguration = device_configuration
    });
    Console.WriteLine("Reported: {0}", startCaptureReply);

    // Wait until the capture has finished
    //  This will take about 5 seconds because we are using a timed capture mode
    // capture.wait()
    ulong Capture_id = startCaptureReply.CaptureInfo.CaptureId;
    Console.WriteLine("\nWait capture #{0}: ", Capture_id);
    var waitCaptureReply = await client.WaitCaptureAsync(new WaitCaptureRequest { CaptureId = Capture_id });
    Console.WriteLine("Reported: {0}", waitCaptureReply);

    // Add an analyzer to the capture
    // Note: The simulator output is not actual SPI data
    // spi_analyzer = capture.add_analyzer('SPI', label = f'Test Analyzer', settings ={
    //    'MISO': 0,
    //    'Clock': 1,
    //    'Enable': 2,
    //    'Bits per Transfer': '8 Bits per Transfer (Standard)'
    //    })
    
    Console.WriteLine("\nAdd SPI analyser: ");
    var add_analyser_SPI_request = new AddAnalyzerRequest
    {
        AnalyzerName = "SPI",
        AnalyzerLabel = "Test Analyser SPI",
        CaptureId = Capture_id,
    };
    add_analyser_SPI_request.Settings.Add("MISO", new AnalyzerSettingValue() { Int64Value = 0 });
    add_analyser_SPI_request.Settings.Add("Clock", new AnalyzerSettingValue() { Int64Value = 1 });
    add_analyser_SPI_request.Settings.Add("Enable", new AnalyzerSettingValue() { Int64Value = 2 });
    add_analyser_SPI_request.Settings.Add("Bits per Transfer", new AnalyzerSettingValue() { StringValue = "8 Bits per Transfer (Standard)" });

    var add_analyzer_reply_SPI =  client.AddAnalyzer(add_analyser_SPI_request);
    Console.WriteLine("Reported: {0}", add_analyzer_reply_SPI);

    ulong analyzer_spi_id = add_analyzer_reply_SPI.AnalyzerId;

    // Store output in a timestamped directory
    // output_dir = os.path.join(os.getcwd(), f'output-{datetime.now().strftime("%Y-%m-%d_%H-%M-%S")}')
    // os.makedirs(output_dir)
    string output_dir = Path.Combine(Directory.GetCurrentDirectory(), "output-" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
    if (!Directory.Exists(output_dir))
    {
        DirectoryInfo di = Directory.CreateDirectory(output_dir);
    }

    // # Export analyzer data to a CSV file
    // analyzer_export_filepath = os.path.join(output_dir, 'spi_export.csv')
    // 
    // capture.export_data_table(
    // filepath = analyzer_export_filepath,
    // analyzers =[spi_analyzer]
    // )
    Console.WriteLine("\nExporting to {0}: ", output_dir);
    var export_request = new ExportDataTableCsvRequest
    {
        CaptureId = Capture_id,
        Filepath = Path.Combine(output_dir, "spi_export.csv")
    };
    export_request.Analyzers.Add(new DataTableAnalyzerConfiguration { AnalyzerId = analyzer_spi_id, RadixType = RadixType.Unspecified });

    var ExportDataTableCsvReply = client.ExportDataTableCsv(export_request);
    Console.WriteLine("Reported: {0}", ExportDataTableCsvReply);

    // Export raw digital data to a CSV file
    // capture.export_raw_data_csv(directory = output_dir, digital_channels =[0, 1, 2, 3])
    Console.WriteLine("\nExporting raw digital data to CSV file");
    LogicChannels export_channels = new LogicChannels();
    export_channels.DigitalChannels.Add(0);
    export_channels.DigitalChannels.Add(1);
    export_channels.DigitalChannels.Add(2);
    export_channels.DigitalChannels.Add(3);

    var export_raw_request = new ExportRawDataCsvRequest
    { 
        CaptureId = Capture_id, 
        Directory = output_dir,
        LogicChannels = export_channels,
        AnalogDownsampleRatio = 1000
        };
    var export_raw_reply = client.ExportRawDataCsv(export_raw_request);
    Console.WriteLine("Reported: {0}", export_raw_reply);

    // Finally, save the capture to a file
    // capture_filepath = os.path.join(output_dir, 'example_capture.sal')
    // capture.save_capture(filepath = capture_filepath)
    Console.WriteLine("\nSave Capture");
    var save_capture_reply = client.SaveCapture(new SaveCaptureRequest
    {
        CaptureId = Capture_id,
        Filepath = Path.Combine(output_dir, "example_capture.sal")
    });
    Console.WriteLine("Reported: {0}", save_capture_reply);

    Console.WriteLine("\nRemove SPI analyser {0}", analyzer_spi_id);
    var remove_analyser_reply_spi = await client.RemoveAnalyzerAsync(new RemoveAnalyzerRequest { AnalyzerId = analyzer_spi_id, CaptureId = Capture_id, });
    analyzer_spi_id = 0;
    Console.WriteLine("Reported: {0}", remove_analyser_reply_spi);

    Console.WriteLine("\nClose Capture {0}", Capture_id);
    var close_capture_reply = await client.CloseCaptureAsync(new CloseCaptureRequest { CaptureId = Capture_id });
    Capture_id = 0;
    Console.WriteLine("Reported: {0}", close_capture_reply);
}
Console.WriteLine("Press any key to exit...");
Console.ReadKey();