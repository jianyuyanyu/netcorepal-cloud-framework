﻿using DotNetCore.CAP;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using NetCorePal.Extensions.Repository.EntityFrameworkCore;

namespace NetCorePal.Extensions.DistributedTransactions.CAP.PostgreSql
{
    public class CapPostgreSqlPublisherTransactionHandler : IPublisherTransactionHandler
    {
        private readonly Lazy<ICapPublisher> _capBus; //lazy load to avoid circular dependency

        public CapPostgreSqlPublisherTransactionHandler(IServiceProvider serviceProvider)
        {
            _capBus = new Lazy<ICapPublisher>(() => serviceProvider.GetRequiredService<ICapPublisher>());
        }

        public async ValueTask<IDbContextTransaction> BeginTransactionAsync(DbContext context)
        {
            return await context.Database.BeginTransactionAsync(_capBus.Value, autoCommit: false);
        }
    }
}