using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using Newtonsoft.Json;

namespace FaceApiCognitiveService
{
    class Program
    {
        static FaceServiceClient _faceServiceClient;
        static DeviceClient _deviceClient;
        static async Task Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Enter filepath to image as argument");
                return;
            }

            // Read settings for appsettings.json file
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json");

            var configuration = builder.Build();

            // Connect the Microsoft Cognitive Services Face API
            _faceServiceClient = new FaceServiceClient(configuration["faceApiKey"], configuration["faceApiEndpointUri"]);

            // Connect to Azure IoT Hub
            _deviceClient = DeviceClient.CreateFromConnectionString(configuration["iotHubDeviceConnectionString"]);
            _deviceClient.ProductInfo = "FaceAPI client";

            // Detect faces in image
            Console.WriteLine($"Detecting faces in image {args[0]} ...");
            var faces = await UploadAndDetectFaces(args[0]);

            Console.WriteLine($"Found {faces.Length} face(s)");
            foreach (var face in faces)
            {
                PrintFaceResult(face);
                Console.WriteLine("Sending face data to IoT Hub...");
                SendMessageToIotHub(face);
            }

            Console.WriteLine("Done");
        }

        static async Task<Face[]> UploadAndDetectFaces(string imageFilePath)
        {
            var faceAttributes = new FaceAttributeType[] { 
                FaceAttributeType.Gender, 
                FaceAttributeType.Age, 
                FaceAttributeType.Smile, 
                FaceAttributeType.Emotion, 
                FaceAttributeType.Glasses, 
                FaceAttributeType.FacialHair };

            try
            {
                using (Stream imageFileStream = File.OpenRead(imageFilePath))
                {
                    Face[] faces = await _faceServiceClient.DetectAsync(imageFileStream, returnFaceId: true, returnFaceLandmarks:false, returnFaceAttributes: faceAttributes);
                    return faces;
                }
            }
            catch (FaceAPIException f)
            {
                Console.WriteLine($"FaceAPI call failed: '{f.ErrorMessage}' with error code {f.ErrorCode}");
                return new Face[0];
            }
            catch (Exception e)
            {
                Console.WriteLine($"FaceAPI call failed: '{e.Message}'");
                return new Face[0];
            }
        }

        static void PrintFaceResult(Face face)
        {
            Console.WriteLine($"Age: {face.FaceAttributes.Age}, Gender: {face.FaceAttributes.Gender}, Glasses: {face.FaceAttributes.Glasses}");
        }

        static async void SendMessageToIotHub(Face face)
        {
            var data = new
            {
                age = face.FaceAttributes.Age,
                gender = face.FaceAttributes.Gender,
                Glasses = face.FaceAttributes.Glasses,
            };
            
            var messageString = JsonConvert.SerializeObject(data);
            var message = new Message(Encoding.ASCII.GetBytes(messageString));

            await _deviceClient.SendEventAsync(message);
        }
    }
}