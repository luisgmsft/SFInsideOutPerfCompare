using Client.BackEnd.Helpers;
using Domain;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Client.BackEnd
{
    public class StatelessBackEndService : IBackEndService
    {
        private static readonly Uri serviceUri;
        private static readonly HttpCommunicationClientFactory communicationClientFactory;

        static StatelessBackEndService()
        {
            serviceUri = new Uri("fabric:/SFInsideOutPerfCompare/BackEnd.Core.Stateless");
            communicationClientFactory = new HttpCommunicationClientFactory();
        }

        public async Task<int> GetSummaryAsync(string slot)
        {
            var summary = 0;

            ServicePartitionClient<HttpCommunicationClient> partitionClient
                        = new ServicePartitionClient<HttpCommunicationClient>(communicationClientFactory, serviceUri, new ServicePartitionKey());

            await partitionClient.InvokeWithRetryAsync(
                async (client) =>
                {
                    HttpResponseMessage httpResponse = await client.HttpClient.GetAsync(new Uri($"{client.Url}api/analysis/summary/{slot}"));
                    httpResponse.EnsureSuccessStatusCode();

                    string content = await httpResponse.Content.ReadAsStringAsync();
                    summary = int.Parse(content);
                });

            return summary;
        }

        public async Task MergeAsync(string slot)
        {
            var jsonAnswer = JsonConvert.SerializeObject(
                new SlotModel
                {
                    Slot = slot
                });

            ServicePartitionClient<HttpCommunicationClient> partitionClient
                        = new ServicePartitionClient<HttpCommunicationClient>(communicationClientFactory, serviceUri, new ServicePartitionKey());

            await partitionClient.InvokeWithRetryAsync(
                async (client) =>
                {
                    HttpResponseMessage httpResponse = await client.HttpClient.PostAsync(new Uri($"{client.Url}api/analysis"), new StringContent(jsonAnswer, Encoding.UTF8, "application/json"));
                    httpResponse.EnsureSuccessStatusCode();
                });
        }
    }
}
