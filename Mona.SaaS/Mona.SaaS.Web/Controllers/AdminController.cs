﻿// MICROSOFT CONFIDENTIAL INFORMATION
//
// Copyright © Microsoft Corporation
//
// Microsoft Corporation (or based on where you live, one of its affiliates) licenses this preview code for your internal testing purposes only.
//
// Microsoft provides the following preview code AS IS without warranty of any kind. The preview code is not supported under any Microsoft standard support program or services.
//
// Microsoft further disclaims all implied warranties including, without limitation, any implied warranties of merchantability or of fitness for a particular purpose. The entire risk arising out of the use or performance of the preview code remains with you.
//
// In no event shall Microsoft be liable for any damages whatsoever (including, without limitation, damages for loss of business profits, business interruption, loss of business information, or other pecuniary loss) arising out of the use of or inability to use the preview code, even if Microsoft has been advised of the possibility of such damages.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mona.AutoIntegration.Interrogators;
using Mona.SaaS.Core.Models.Configuration;
using Mona.SaaS.Web.Models;
using Mona.SaaS.Web.Models.Admin;
using Mona.SaaS.Web.Models.Admin.LogicApps;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Mona.SaaS.Web.Controllers
{
    [Authorize(Policy = "admin")]
    public class AdminController : Controller
    {
        private readonly DeploymentConfiguration deploymentConfig;
        private readonly IdentityConfiguration identityConfig;
        private readonly OfferConfiguration offerConfig;
        private readonly ILogger logger;

        public AdminController(
            IOptionsSnapshot<DeploymentConfiguration> deploymentConfig,
            IOptionsSnapshot<IdentityConfiguration> identityConfig,
            OfferConfiguration offerConfig,
            ILogger<AdminController> logger)
        {
            this.deploymentConfig = deploymentConfig.Value;
            this.identityConfig = identityConfig.Value;
            this.offerConfig = offerConfig;
            this.logger = logger;
        }

        [HttpGet, Route("admin", Name = "admin")]
        public async Task<IActionResult> Index()
        {
            try
            {
                if (this.offerConfig.IsSetupComplete == false)
                {
                    return RedirectToRoute("setup");
                }

                var adminModel = new AdminPageModel
                {
                    IsTestModeEnabled = this.deploymentConfig.IsTestModeEnabled,
                    MonaVersion = this.deploymentConfig.MonaVersion,
                    AzureSubscriptionId = this.deploymentConfig.AzureSubscriptionId,
                    AzureResourceGroupName = this.deploymentConfig.AzureResourceGroupName,
                    EventGridTopicOverviewUrl = GetEventGridTopicUrl(),
                    IntegrationPlugins = (await GetAvailableIntegrationPluginModels()).ToList(),
                    ConfigurationSettingsEditorUrl = GetConfigurationSettingsEditorUrl(),
                    PartnerCenterTechnicalDetails = GetPartnerCenterTechnicalDetails(),
                    ResourceGroupOverviewUrl = GetResourceGroupUrl(),
                    TestLandingPageUrl = Url.RouteUrl("landing/test", null, Request.Scheme),
                    TestWebhookUrl = Url.RouteUrl("webhook/test", null, Request.Scheme),
                    LogicAppSnippets = GetLogicAppSnippetUrls()
                };

                return View(adminModel);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "An error occurred while attempting to load the Mona admin page. See exception for details.");

                throw;
            }
        }

        [HttpGet, Route("/admin/logic-apps/action-templates/activated", Name = "logic-apps/action-templates/activated")]
        public IActionResult GetActivatedLogicAppActionTemplate() => SnippetJson(
            new HttpAction<ActivatedHttpActionInputs>(new ActivatedHttpActionInputs(GetMarketplaceAuthenticationMetadata())),
            "Notify Marketplace of subscription activation");

        [HttpGet, Route("/admin/logic-apps/action-templates/operation-failed", Name = "logic-apps/action-templates/operation-failed")]
        public IActionResult GetFailedOperationUpdateLogicAppActionTemplate() => SnippetJson(
            new HttpAction<OperationUpdateHttpActionInputs>(new OperationUpdateHttpActionInputs(GetMarketplaceAuthenticationMetadata(), false)),
            "Notify Marketplace of failed operation");

        [HttpGet, Route("/admin/logic-apps/action-templates/operation-succeeded", Name = "logic-apps/action-templates/operation-succeeded")]
        public IActionResult GetSuccessfulOperationUpdateLogicAppActionTemplate() => SnippetJson(
            new HttpAction<OperationUpdateHttpActionInputs>(new OperationUpdateHttpActionInputs(GetMarketplaceAuthenticationMetadata(), true)),
            "Notify Marketplace of successful operation");

        private JsonResult SnippetJson(object data, string snippetTitle) => Json(
            new Dictionary<string, object> { [snippetTitle] = data },
            new JsonSerializerSettings { Formatting = Formatting.Indented });

        private LogicAppSnippetsModel GetLogicAppSnippetUrls() =>
            new LogicAppSnippetsModel
            {
                ActivationSnippetUrl = Url.RouteUrl("logic-apps/action-templates/activated", null, Request.Scheme),
                OperationFailedUrl = Url.RouteUrl("logic-apps/action-templates/operation-failed", null, Request.Scheme),
                OperationSucceededUrl = Url.RouteUrl("logic-apps/action-templates/operation-succeeded", null, Request.Scheme)
            };

        private MarketplaceAuthenticationMetadata GetMarketplaceAuthenticationMetadata() =>
            new MarketplaceAuthenticationMetadata
            {
               ClientId = this.identityConfig.AppIdentity.AadClientId,
               Secret = this.identityConfig.AppIdentity.AadClientSecret,
               TenantId = this.identityConfig.AppIdentity.AadTenantId
            };

        private string GetConfigurationSettingsEditorUrl() =>
            $"https://portal.azure.com/#@{this.identityConfig.AppIdentity.AadTenantId}/resource/subscriptions/{this.deploymentConfig.AzureSubscriptionId}" +
            $"/resourceGroups/{this.deploymentConfig.AzureResourceGroupName}/providers/Microsoft.AppConfiguration" +
            $"/configurationStores/mona-config-{this.deploymentConfig.Name.ToLower()}/kvs";

        private string GetEventGridTopicUrl() =>
            $"https://portal.azure.com/#@{this.identityConfig.AppIdentity.AadTenantId}/resource/subscriptions/{this.deploymentConfig.AzureSubscriptionId}" +
            $"/resourceGroups/{this.deploymentConfig.AzureResourceGroupName}/providers/Microsoft.EventGrid" +
            $"/topics/mona-events-{this.deploymentConfig.Name.ToLower()}/overview";

        private string GetResourceGroupUrl() =>
            $"https://portal.azure.com/#@{this.identityConfig.AppIdentity.AadTenantId}/resource/subscriptions/{this.deploymentConfig.AzureSubscriptionId}" +
            $"/resourceGroups/{this.deploymentConfig.AzureResourceGroupName}/overview";

        private PartnerCenterTechnicalDetails GetPartnerCenterTechnicalDetails() =>
            new PartnerCenterTechnicalDetails
            {
                AadApplicationId = this.identityConfig.AppIdentity.AadClientId,
                AadTenantId = this.identityConfig.AppIdentity.AadTenantId,
                LandingPageUrl = Url.RouteUrl("landing", null, Request.Scheme),
                WebhookUrl = Url.RouteUrl("webhook", null, Request.Scheme)
            };

        private async Task<IEnumerable<PluginModel>> GetAvailableIntegrationPluginModels()
        {
            var locale = CultureInfo.CurrentUICulture.Name;

            var azCredentials = SdkContext.AzureCredentialsFactory.FromServicePrincipal(
                identityConfig.AppIdentity.AadClientId,
                identityConfig.AppIdentity.AadClientSecret,
                identityConfig.AppIdentity.AadTenantId,
                AzureEnvironment.AzureGlobalCloud);

            var logicAppInterrogator = new LogicAppPluginInterrogator();

            var plugins = await logicAppInterrogator.InterrogateResourceGroupAsync(
                azCredentials, this.deploymentConfig.AzureSubscriptionId, this.deploymentConfig.AzureResourceGroupName);

            var pluginModels = plugins
                .Select(p => new PluginModel
                {
                    Description = p.Description.GetLocalPropertyValue(locale),
                    DisplayName = p.DisplayName.GetLocalPropertyValue(locale),
                    EditorUrl = p.EditorUrl,
                    Id = p.Id,
                    ManagementUrl = p.ManagementUrl,
                    PluginType = p.PluginType,
                    Status = p.Status,
                    TriggerEventType = p.TriggerEventType,
                    TriggerEventVersion = p.TriggerEventVersion,
                    Version = p.Version
                })
                .ToList();

            return pluginModels;
        }
    }
}