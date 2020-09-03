using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core;
using Exceptionless.Core.Migrations;
using Exceptionless.Core.Repositories;
using Exceptionless.Tests.Utility;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Repositories.Migrations;
using Foundatio.Utility;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Migrations {
    public class SetStackDuplicateSignatureMigrationTests : TestWithServices {
        private readonly IStackRepository _repository;
        
        public SetStackDuplicateSignatureMigrationTests(ITestOutputHelper output) : base(output) {
            _repository = GetService<IStackRepository>();
        }

        protected override void RegisterServices(IServiceCollection services, AppOptions options) {
            services.AddTransient<SetStackDuplicateSignature>();
            services.AddSingleton<ILock>(new EmptyLock());
            base.RegisterServices(services, options);
        }

        [Fact]
        public async Task WillSetStackDuplicateSignature() {
            var stack =  await _repository.AddAsync(StackData.GenerateStack(), o => o.ImmediateConsistency());
            Assert.NotEmpty(stack.ProjectId);
            Assert.NotEmpty(stack.SignatureHash);
            Assert.Null(stack.DuplicateSignature);
            
            var migration = GetService<SetStackDuplicateSignature>();
            var context = new MigrationContext(GetService<ILock>(), _logger, CancellationToken.None);
            await migration.RunAsync(context);

            string expectedDuplicateSignature = $"{stack.ProjectId}:{stack.SignatureHash}";
            var actualStack = await _repository.GetByIdAsync(stack.Id);
            Assert.NotEmpty(actualStack.ProjectId);
            Assert.NotEmpty(actualStack.SignatureHash);
            Assert.Equal($"{actualStack.ProjectId}:{actualStack.SignatureHash}", actualStack.DuplicateSignature);

            var results = await _repository.GetStackByDuplicateSignatureAsync(expectedDuplicateSignature);
            Assert.Single(results.Documents);
        }
    }
}