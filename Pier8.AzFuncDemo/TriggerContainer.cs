using System;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Management.ContainerInstance.Fluent;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Extensions.Logging;

namespace Pier8.AzFuncDemo
{
    public static class TriggerContainer
    {
        private static readonly Region Region = Region.UKSouth;
        
        [FunctionName("TriggerContainer")]
        public static async Task<IActionResult> RunAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)]
            HttpRequest req, ILogger log)
        {
            try
            {
                var credentials = SdkContext.AzureCredentialsFactory.FromFile(Environment.GetEnvironmentVariable("AZURE_AUTH_LOCATION"));

                var azure = await Azure
                    .Configure()
                    .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
                    .Authenticate(credentials)
                    .WithDefaultSubscriptionAsync();

                // Print selected subscription
                log.LogInformation("Selected subscription: " + azure.SubscriptionId);

                RunSample(azure, log);
                return new OkResult();
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Unexpected Error");
                return new InternalServerErrorResult();
            }
        }

        private static void RunSample(IAzure azure, ILogger log)
        {
            var rgName = SdkContext.RandomResourceName("rgACI", 15);
            var aciName = SdkContext.RandomResourceName("acisample", 20);
            var shareName = SdkContext.RandomResourceName("fileshare", 20);
            string containerImageName = "seanmckenna/aci-hellofiles";
            string volumeMountName = "aci-helloshare";

            try
            {
                var containerGroup = azure.ContainerGroups.Define(aciName)
                    .WithRegion(Region)
                    .WithNewResourceGroup(rgName)
                    .WithLinux()
                    .WithPublicImageRegistryOnly()
                    .WithNewAzureFileShareVolume(volumeMountName, shareName)
                    .DefineContainerInstance(aciName)
                        .WithImage(containerImageName)
                        .WithExternalTcpPort(80)
                        .WithVolumeMountSetting(volumeMountName, "/aci/logs/")
                        .Attach()
                    .WithDnsPrefix(aciName)
                    .Create();

                Print(containerGroup, log);

                SdkContext.DelayProvider.Delay(20000);
                log.LogInformation("Container instance IP address: " + containerGroup.IPAddress);

                containerGroup = azure.ContainerGroups.GetByResourceGroup(rgName, aciName);
                var logContent = containerGroup.GetLogContent(aciName);
                log.LogInformation($"Logs for container instance: {aciName}\n{logContent}");

                azure.ContainerGroups.DeleteById(containerGroup.Id);
            }
            finally
            {
                try
                {
                    log.LogInformation("Deleting Resource Group: " + rgName);
                    azure.ResourceGroups.BeginDeleteByName(rgName);
                    log.LogInformation("Deleted Resource Group: " + rgName);
                }
                catch (Exception)
                {
                    log.LogError("Did not create any resources in Azure. No clean up is necessary");
                }
            }
        }
        
        public static void Print(IContainerGroup containerGroup, ILogger log)
        {
            var info = new StringBuilder().Append("Container Group: ").Append(containerGroup.Id)
                .Append("Name: ").Append(containerGroup.Name)
                .Append("\n\tResource group: ").Append(containerGroup.ResourceGroupName)
                .Append("\n\tRegion: ").Append(containerGroup.RegionName)
                .Append("\n\tTags: ").Append(containerGroup.Tags)
                .Append("\n\tOS type: ").Append(containerGroup.OSType.Value);

            if (containerGroup.IPAddress != null)
            {
                info.Append("\n\tPublic IP address: ").Append(containerGroup.IPAddress);
                info.Append("\n\tExternal TCP ports:");
                foreach (int port in containerGroup.ExternalTcpPorts)
                {
                    info.Append(" ").Append(port);
                }
                info.Append("\n\tExternal UDP ports:");
                foreach (int port in containerGroup.ExternalUdpPorts)
                {
                    info.Append(" ").Append(port);
                }
            }
            if (containerGroup.ImageRegistryServers.Count > 0)
            {
                info.Append("\n\tPrivate Docker image registries:");
                foreach (string server in containerGroup.ImageRegistryServers)
                {
                    info.Append(" ").Append(server);
                }
            }
            if (containerGroup.Volumes.Count > 0)
            {
                info.Append("\n\tVolume mapping: ");
                foreach (var entry in containerGroup.Volumes)
                {
                    info.Append("\n\t\tName: ").Append(entry.Key).Append(" -> ").Append(entry.Value.AzureFile.ShareName);
                }
            }
            if (containerGroup.Containers.Count > 0)
            {
                info.Append("\n\tContainer instances: ");
                foreach (var entry in containerGroup.Containers)
                {
                    var container = entry.Value;
                    info.Append("\n\t\tName: ").Append(entry.Key).Append(" -> ").Append(container.Image);
                    info.Append("\n\t\t\tResources: ");
                    info.Append(container.Resources.Requests.Cpu).Append(" CPUs ");
                    info.Append(container.Resources.Requests.MemoryInGB).Append(" GB");
                    info.Append("\n\t\t\tPorts:");
                    foreach (var port in container.Ports)
                    {
                        info.Append(" ").Append(port.Port);
                    }
                    if (container.VolumeMounts != null)
                    {
                        info.Append("\n\t\t\tVolume mounts:");
                        foreach (var volumeMount in container.VolumeMounts)
                        {
                            info.Append(" ").Append(volumeMount.Name).Append("->").Append(volumeMount.MountPath);
                        }
                    }
                    if (container.Command != null)
                    {
                        info.Append("\n\t\t\tStart commands:");
                        foreach (var command in container.Command)
                        {
                            info.Append("\n\t\t\t\t").Append(command);
                        }
                    }
                    if (container.EnvironmentVariables != null)
                    {
                        info.Append("\n\t\t\tENV vars:");
                        foreach (var envVar in container.EnvironmentVariables)
                        {
                            info.Append("\n\t\t\t\t").Append(envVar.Name).Append("=").Append(envVar.Value);
                        }
                    }
                }
            }

            log.LogInformation(info.ToString());
        }

    }
}