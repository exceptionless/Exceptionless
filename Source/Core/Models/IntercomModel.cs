#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Security.Cryptography;
using System.Text;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Models;
using MongoDB.Bson;

namespace Exceptionless.Core.Models {
    public class IntercomModel {
        public IntercomModel(User user, Organization organization) {
            if (user == null)
                throw new ArgumentNullException("user");

            UserId = user.Id;
            UserEmail = user.EmailAddress;
            UserHash = HMACSHA256HashString(user.Id);
            UserCreated = GetTimestamp(user.Id);

            if (organization == null)
                return;

            HasCompany = true;
            CompanyId = organization.Id;
            CompanyName = organization.Name;
            CompanyCreated = GetTimestamp(organization.Id);
            Plan = organization.PlanId;
            BillingPrice = organization.BillingPrice;
            TotalErrors = organization.TotalEventCount;
            if (organization.SubscribeDate.HasValue)
                SubscribeDate = organization.SubscribeDate.Value.ToEpoch();
        }

        public string UserId { get; private set; }
        public string UserEmail { get; private set; }
        public string UserHash { get; private set; }
        public int UserCreated { get; private set; }
        public bool HasCompany { get; private set; }
        public string CompanyId { get; private set; }
        public string CompanyName { get; private set; }
        public int CompanyCreated { get; private set; }
        public string Plan { get; private set; }
        public long TotalErrors { get; private set; }
        public decimal BillingPrice { get; private set; }
        public int SubscribeDate { get; private set; }

        private static DateTime GetCreatedDate(string objectId) {
            var id = new ObjectId(objectId);
            return id.CreationTime;
        }

        private static int GetTimestamp(string objectId) {
            DateTime dt = GetCreatedDate(objectId);
            return dt.ToEpoch();
        }

        private static string HMACSHA256HashString(string value) {
            const string intercomApiSecret = "N9VVg6v_CevW9EaMkPBa5yRCit-yuBEBfpO5pLDs";
            byte[] secretKey = Encoding.UTF8.GetBytes(intercomApiSecret);
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            using (var hmac = new HMACSHA256(secretKey)) {
                hmac.ComputeHash(bytes);
                byte[] data = hmac.Hash;

                var builder = new StringBuilder();
                for (int i = 0; i < data.Length; i++)
                    builder.Append(data[i].ToString("x2"));

                return builder.ToString();
            }
        }
    }
}