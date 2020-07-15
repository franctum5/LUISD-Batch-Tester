using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.FileExtensions;
using Microsoft.Extensions.Configuration.Json;
using System.Threading;
using LuisPredict.Sdk;
using System.Threading.Tasks;
using System.IO;

namespace LuisDBatchTester
{
    class Program
    {
        private readonly static HttpClient httpClient = new HttpClient();
        public static async Task Main(string[] args)
        {
            IConfiguration config = new ConfigurationBuilder()
            .AddJsonFile("app.settings.json", true, true)
            .Build();



            // Create the LUIS prediction client with the endpoint information.
            using var client = new LuisPredictionClient(new Uri(config["EndpointBaseUri"]), config["LUISPredictionKey"]);


            // *** EXAMPLE #1: RUN MODEL ON THE SPECIFIED TEXT **
            // Run prediction on text using the specified model. The maximum token count for the text is 4000 tokens.
            //const string Text = @"Hello. I'd like to repeat my takeout food order from last weekend at Casa Sanchez.Hello, How can I help you?Okay sure thing let me go ahead and repeat what you got from Casa Sanchez so it looks like you got 1 market burrito with black beans and spicy salsa and 1 chicken taco platter with refried beans.Yep. That's right.Perfect, What would you like to add?I'd like one salad with house dressing.And anything else.No, thanks.That is all, and it is correct.Okay you got it, Your order will be ready for pick-up in about 20 minutes.Sounds good. Thank you for your help.You got it, Take care."; // <-- SET THIS TO THE TEXT ON WHICH YOU WANT TO PREDICT.
            //Console.WriteLine($"Running model on text: \"{Text}\"");
            //var result = await client.PredictAsync(Text, Guid.Parse(config["LUISAppId"]), (PublishSlot)Enum.Parse(typeof(PublishSlot), config["ModelSlot"]));
            //Console.WriteLine("\nModel output:");



            // *** EXAMPLE #2: CONVERT ALL DOCUMENTS IN THE SPECIFIED LOCAL FOLDER AND RUN MODEL ON THE CONVERTED TEXT **
            // Convert a file to text. The conversion API supports the following extensions: .pdf, .docx, .pptx, .eml, .msg, .html.
            // The return type is an array of strings because the conversion API splits the results into chunks if there are more than 4000 tokens.


            string[] files = Directory.GetFiles(config["TestFilesFolder"]);

            string outputFolder = config["TestFilesFolder"] + "\\output";
            Directory.CreateDirectory(outputFolder);

            foreach (string file in files)
            {
                PredictionResult luisResult = null;
                if (!file.EndsWith(".txt"))
                {
                    Console.WriteLine($"\nConverting file: {file}");
                    var convertedText = await client.ConvertToTextAsync(file);
                    Console.WriteLine("\nConversion output:");
                    Console.WriteLine(string.Concat(convertedText));

                    // Run prediction on each 4000 token chunk of converted text.
                    foreach (var chunkText in convertedText)
                    {
                        Console.WriteLine($"\nRunning model on text: \"{chunkText}\"");
                        luisResult = await client.PredictAsync(chunkText, Guid.Parse(config["LUISAppId"]), (PublishSlot)Enum.Parse(typeof(PublishSlot), config["ModelSlot"]));
                        Console.WriteLine("\nModel output:");

                    }
                }
                else
                {
                    Console.WriteLine($"\nReading content of txt file: {file}");
                    var txtContent = File.ReadAllText(file);
                    luisResult = await client.PredictAsync(txtContent, Guid.Parse(config["LUISAppId"]), (PublishSlot)Enum.Parse(typeof(PublishSlot), config["ModelSlot"]));
                }


                string jsonResult = JsonConvert.SerializeObject(luisResult);

                File.WriteAllText($"{outputFolder}\\{Path.GetFileNameWithoutExtension(file)}.output.json", jsonResult);
                
            }



        }
    }
}
