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

using Newtonsoft.Json;

namespace Mona.SaaS.Web.Models.Admin.LogicApps
{
    public class OperationUpdateHttpActionInputs : BaseHttpActionInputs
    {
        public OperationUpdateHttpActionInputs()
        {
            Method = "PATCH";

            Uri = "https://marketplaceapi.microsoft.com/api/saas/subscriptions/@{body('Parse_event_information_(1)')?['Subscription ID']}/operations/@{body('Parse_event_information_(1)')?['Operation ID']}?api-version=2018-03-01";
        }

        public OperationUpdateHttpActionInputs(MarketplaceAuthenticationMetadata authMetadata, bool isSuccessful) : this()
        {
            AuthMetadata = authMetadata;
            Body = new OperationUpdateHttpBody(isSuccessful);
        }

        [JsonProperty("body")]
        public OperationUpdateHttpBody Body { get; set; }
    }
}
