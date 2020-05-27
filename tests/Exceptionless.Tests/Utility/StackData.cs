using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Exceptionless.Core.Models;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Foundatio.Repositories;
using Foundatio.Repositories.Utility;
using Newtonsoft.Json;
using Xunit;

namespace Exceptionless.Tests.Utility {
    internal static class StackData {
        public static IEnumerable<Stack> GenerateStacks(int count = 10, bool generateId = false, string id = null, string organizationId = null, string projectId = null, string type = null) {
            for (int i = 0; i < count; i++)
                yield return GenerateStack(generateId, id, organizationId, projectId, type: type);
        }

        public static List<Stack> GenerateSampleStacks() {
            return new List<Stack> {
                GenerateSampleStack(),
                GenerateStack(id: TestConstants.StackId2, organizationId: TestConstants.OrganizationId, projectId: TestConstants.ProjectIdWithNoRoles),
                GenerateStack(generateId: true, organizationId: TestConstants.OrganizationId)
            };
        }

        public static Stack GenerateSampleStack(string id = TestConstants.StackId) {
            return GenerateStack(id: id, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId);
        }

        public static Stack GenerateStack(bool generateId = false, string id = null, string organizationId = null, string projectId = null, string type = null, string title = null, DateTime? dateFixed = null, DateTime? utcFirstOccurrence = null, DateTime? utcLastOccurrence = null, int totalOccurrences = 0, StackStatus status = StackStatus.Open, string signatureHash = null) {
            var stack = new Stack {
                Id = id.IsNullOrEmpty() ? generateId ? ObjectId.GenerateNewId().ToString() : null : id,
                OrganizationId = organizationId.IsNullOrEmpty() ? TestConstants.OrganizationId : organizationId,
                ProjectId = projectId.IsNullOrEmpty() ? TestConstants.ProjectIds.Random() : projectId,
                Title = title ?? RandomData.GetTitleWords(),
                Type = type ?? Stack.KnownTypes.Error,
                DateFixed = dateFixed,
                FirstOccurrence = utcFirstOccurrence ?? DateTime.MinValue,
                LastOccurrence = utcLastOccurrence ?? DateTime.MinValue,
                TotalOccurrences = totalOccurrences,
                Status = status,
                SignatureHash = signatureHash ?? RandomData.GetAlphaNumericString(10, 10),
                SignatureInfo = new SettingsDictionary()
            };

            if (type == Event.KnownTypes.Error)
                stack.SignatureInfo.Add("ExceptionType", TestConstants.ExceptionTypes.Random());

            for (int i = 0; i < RandomData.GetInt(0, 5); i++) {
                string tag = RandomData.GetWord();
                while (stack.Tags.Contains(tag))
                    tag = RandomData.GetWord();

                stack.Tags.Add(tag);
            }

            return stack;
        }
        
        public static  async Task CreateSearchDataAsync(IStackRepository stackRepository, JsonSerializer serializer) {
            string path = Path.Combine("..", "..", "..", "Search", "Data");
            foreach (string file in Directory.GetFiles(path, "stack*.json", SearchOption.AllDirectories)) {
                if (file.EndsWith("summary.json"))
                    continue;

                using (var stream = new FileStream(file, FileMode.Open)) {
                    using (var streamReader = new StreamReader(stream)) {
                        var stack = serializer.Deserialize(streamReader, typeof(Stack)) as Stack;
                        Assert.NotNull(stack);
                        await stackRepository.AddAsync(stack, o => o.ImmediateConsistency());
                    }
                }
            }
        }

    }
}