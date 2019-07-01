using Google.Cloud.Speech.V1;
using Google.Cloud.Storage.V1;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using System.Configuration;
using Google.Apis.Storage.v1.Data;
using Newtonsoft.Json;
using Twilio.TwiML;
using Twilio.TwiML.Voice;
using Twilio.Http;

namespace TwilioApp
{
    class Program
    {
        private static readonly string GoogleApplicationCredentialsKey = "GOOGLE_APPLICATION_CREDENTIALS";
        /// <summary> 
        /// This file is created in Google Console
        /// </summary>
        private static string GoogleApplicationCredentialsPath { get; } = Environment.CurrentDirectory + "/twilioapp-217106-cf04fa68faf9.json";
        /// <summary>
        /// Name of the bucket is setted in Google Console
        /// </summary>
        private static string GoogleBucketName { get; } = "twiliospeechtotext";

        static void Main(string[] args)
        {
            Environment.SetEnvironmentVariable(GoogleApplicationCredentialsKey, GoogleApplicationCredentialsPath);
            Call();
        }

        /// <summary>
        /// This Functions goes to Twilio, make a call, Twilio save a record of this call then goes to Google Speech to text API and transcribe the call saving a txt local file
        /// </summary>
        public static void Call()
        {
            Console.WriteLine("Init Twilio API...");
            ///Twilio SID and Token
            const string accountSid = "ACa62c3db32794048447xxxxxxxxxxxx";
            const string authToken = "f420e52ee6774e0898fdxxxxxxxxxxxx";

            TwilioClient.Init(accountSid, authToken);
            Console.WriteLine("Let's make a call, please provide me your phone number (format: +AreaNumner)");
            var phoneNumber = Console.ReadLine();
            var call = CallResource.Create(
                record: true,
                url: new Uri("https://corn-collie-1715.twil.io/assets/Voice.xml"), //is a best practice to upload the assets in twilio account
                to: new Twilio.Types.PhoneNumber(phoneNumber),
                from: new Twilio.Types.PhoneNumber("+000000000") //Twilio number created in you account
            );

            RecordingResource recordings;
            RecordingResource.StatusEnum recordingStatus;

            do
            {
                recordings = RecordingResource.Read().Where(x => x.CallSid == call.Sid).FirstOrDefault();
            } while (recordings == null);

            do
            {
                Console.WriteLine("Processing Recording....");
                 recordingStatus = RecordingResource.Read().Where(x => x.CallSid == call.Sid).Select(x => x.Status).FirstOrDefault();
            } while (recordingStatus == RecordingResource.StatusEnum.Processing);

            WebClient wc = new WebClient();
            wc.DownloadFile(@"https://api.twilio.com/" + recordings.Uri.Replace("json", "wav"), recordings.Sid + ".wav");
            Console.WriteLine("Now we have the recording,Lets sync with Google Services, please wait... ");
            string audioDirectory = Path.Combine(Environment.CurrentDirectory,  recordings.Sid + ".wav");
            var memoryStream = new MemoryStream();
            using (var file = new FileStream(audioDirectory, FileMode.Open, FileAccess.Read))
                file.CopyTo(memoryStream);

            var speechClient = SpeechClient.Create();
            var storageClient = StorageClient.Create();

            //We have to upload the file to google storage before transcribe
            var uploadedWavFile = storageClient.UploadObject(GoogleBucketName, recordings.Sid + ".wav", "audio/wav", memoryStream);

            //Get the file
            var storageObject = storageClient.GetObject(GoogleBucketName, recordings.Sid + ".wav");
            var storageUri = $@"gs://{GoogleBucketName}/{storageObject.Name}";
            storageObject.Acl = storageObject.Acl ?? new List<ObjectAccessControl>();
            storageClient.UpdateObject(storageObject, new UpdateObjectOptions
            {
                PredefinedAcl = PredefinedObjectAcl.PublicRead
            });

            Console.WriteLine("We will start to transcribe your recording, this operation will take few moments...");
            //Speech to Text operation
            var longOperation = speechClient.LongRunningRecognize(new RecognitionConfig()
            {
                //the properties below are not the required for MP3 files and that's why the opertion returns null, we can make this more 
                //generic knowing what kind of properties we need for each file type or standarize the result just for one type.
                //Encoding = RecognitionConfig.Types.AudioEncoding.Linear16,
                //SampleRateHertz = 44100,
                EnableWordTimeOffsets = true,
                LanguageCode = "en-US"
            }, RecognitionAudio.FromStorageUri(storageUri));

            longOperation = longOperation.PollUntilCompleted();
            //TODO: fix this implementation. Sometimes there is a null being returned from longOperation.Result
            var response = longOperation.Result;
            if (response != null && response.Results != null && response.Results.Count > 0)
            {
                Console.WriteLine("Is done!, now we will create a file with the complete transcription for you...");
                //var resultArray = (JsonConvert.DeserializeObject<RootObject>(response.Results.ToString()));
                foreach(var res in response.Results)
                {
                    string transcription = res.Alternatives.Select(x => x.Transcript).FirstOrDefault();
                    File.AppendAllText(Path.Combine(Environment.CurrentDirectory, recordings.Sid + ".txt"), transcription);
                }

            }
            Console.WriteLine("File Created!, Now we will clean our directories and give you the path of the mentioned file...");
            storageClient.DeleteObject(GoogleBucketName, storageObject.Name);

            if (File.Exists(Path.Combine(Environment.CurrentDirectory, recordings.Sid + ".wav")))
            {
                File.Delete(Path.Combine(Environment.CurrentDirectory, recordings.Sid + ".wav"));
            }

            Console.WriteLine("You can find your txt file here: " + Path.Combine(Environment.CurrentDirectory, recordings.Sid + ".txt"));
            Console.ReadLine();
        }
    }
}
