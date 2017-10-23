using Client.BackEnd.Helpers;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Query;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Client.BackEnd
{
    public class StatefulBackEndService : IBackEndService
    {
        private const int MaxQueryRetryCount = 20;

        private static readonly Uri serviceUri;
        private static readonly FabricClient fabricClient;
        private static readonly HttpCommunicationClientFactory communicationClientFactory;
        private static readonly TimeSpan BackoffQueryDelay = TimeSpan.FromSeconds(1);

        static StatefulBackEndService()
        {
            serviceUri = new Uri("fabric:/SFInsideOutPerfCompare/BackEnd.Core.Stateful");
            fabricClient = new FabricClient();
            communicationClientFactory =
                new HttpCommunicationClientFactory(new ServicePartitionResolver(() => fabricClient));
        }

        public async Task<int> GetSummaryAsync(string slot)
        {
            var summary = 0;

            foreach (var partition in await this.GetServicePartitionKeysAsync())
            {
                try
                {
                    ServicePartitionClient<HttpCommunicationClient> partitionClient
                        = new ServicePartitionClient<HttpCommunicationClient>(communicationClientFactory, serviceUri, new ServicePartitionKey(partition.LowKey));

                    await partitionClient.InvokeWithRetryAsync(
                        async (client) =>
                        {
                            HttpResponseMessage httpResponse = await client.HttpClient.GetAsync(new Uri($"{client.Url}/api/analysis/summary/{slot}"));
                            httpResponse.EnsureSuccessStatusCode();

                            string content = await httpResponse.Content.ReadAsStringAsync();
                            summary = int.Parse(content);
                        });

                    break;
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }

            return summary;
        }

        public async Task MergeAsync(string slot)
        {
            var jsonAnswer = JsonConvert.SerializeObject(
                new {
                    @slot = slot
                });

            foreach (var partition in await this.GetServicePartitionKeysAsync())
            {
                try
                {
                    ServicePartitionClient<HttpCommunicationClient> partitionClient
                        = new ServicePartitionClient<HttpCommunicationClient>(communicationClientFactory, serviceUri, new ServicePartitionKey(partition.LowKey));

                    await partitionClient.InvokeWithRetryAsync(
                        async (client) =>
                        {
                            HttpResponseMessage httpResponse = await client.HttpClient.PostAsync(new Uri($"{client.Url}/api/analysis"), new StringContent(jsonAnswer, Encoding.UTF8, "application/json"));
                            httpResponse.EnsureSuccessStatusCode();
                        });

                    break;
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }

        private async Task<IList<Int64RangePartitionInformation>> GetServicePartitionKeysAsync()
        {
            for (int i = 0; i < MaxQueryRetryCount; i++)
            {
                try
                {
                    // Get the list of partitions up and running in the service.

                    ServicePartitionList partitionList = await fabricClient.QueryManager.GetPartitionListAsync(serviceUri);

                    // For each partition, build a service partition client used to resolve the low key served by the partition.

                    IList<Int64RangePartitionInformation> partitionKeys = new List<Int64RangePartitionInformation>(partitionList.Count);

                    foreach (Partition partition in partitionList)
                    {
                        Int64RangePartitionInformation partitionInfo = partition.PartitionInformation as Int64RangePartitionInformation;

                        if (partitionInfo == null)
                        {
                            throw new InvalidOperationException(
                                string.Format(
                                    "The service {0} should have a uniform Int64 partition. Instead: {1}",
                                    serviceUri.ToString(),
                                    partition.PartitionInformation.Kind));
                        }

                        partitionKeys.Add(partitionInfo);
                    }

                    return partitionKeys;
                }
                catch (FabricTransientException ex)
                {
                    //ServiceEventSource.Current.OperationFailed(ex.Message, "create representative partition clients");
                    if (i == MaxQueryRetryCount - 1)
                    {
                        throw;
                    }
                }
                await Task.Delay(BackoffQueryDelay);
            }

            throw new TimeoutException("Retry timeout is exhausted and creating representative partition clients wasn't successful");
        }
    }
}
