﻿using Microsoft.EntityFrameworkCore;
using Stormancer.Server.Plugins.Database.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Users.Data
{
    internal class UserDbModelBuilder : IDbModelBuilder
    {
        public void OnModelCreating(ModelBuilder modelBuilder, string contextId, Dictionary<string, object> customData)
        {
            
            modelBuilder.Entity<UserRecord>().HasIndex(b => b.UserHandle)
                .IsUnique();
            modelBuilder.Entity<UserRecord>().HasMany(u => u.Identities).WithMany(i => i.Users);

            modelBuilder.Entity<IdentityRecord>().HasOne(i => i.MainUser).WithMany(u=>u.MainIdentities);
        }
    }
}
