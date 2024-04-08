using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using Stormancer.Diagnostics;
using Stormancer.Server.Plugins.Database.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Users.EntityFramework
{
    internal class EntityFrameworkUserStorage : IUserStorage
    {
        private readonly DbContextAccessor _dbContext;
        private readonly ILogger _logger;

        public EntityFrameworkUserStorage(DbContextAccessor dbContext, ILogger logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<User> CreateUser(User user)
        {
            var record = UserRecord.CreateRecordFromUser(user);

            var dbContext = await this._dbContext.GetDbContextAsync();
            await dbContext.Set<UserRecord>().AddAsync(record);

            await dbContext.SaveChangesAsync();
            return user;
        }

        public async Task Delete(string id)
        {
            var dbContext = await _dbContext.GetDbContextAsync();

            var guid = Guid.Parse(id);

            var record = await dbContext.Set<UserRecord>().SingleOrDefaultAsync(u => u.Id == guid);

            if (record != null)
            {
                dbContext.Set<UserRecord>().Remove(record);
                await dbContext.SaveChangesAsync();
            }
        }

        public async Task<(User user, bool added)> AddAuthentication(User user, string provider, string identifier, Action<dynamic> authDataModifier)
        {
            var c = await _dbContext.GetDbContextAsync();

            var auth = user.Auth[provider];
            if (auth == null)
            {
                auth = new JObject();
                user.Auth[provider] = auth;
            }
            authDataModifier?.Invoke(auth);

            var userId = Guid.Parse(user.Id);
            var record = c.Set<UserRecord>().Include(u => u.Identities.Where(i => i.Provider == provider && i.Identity == identifier)).SingleOrDefault(u => u.Id == userId);


            var identity = record.Identities.FirstOrDefault();
            var added = identity == null;
            if (identity == null)
            {
                identity = new IdentityRecord { Provider = provider, Identity = identifier };
                identity.Users = new List<UserRecord>
                {
                    record
                };

                await c.Set<IdentityRecord>().AddAsync(identity);
            }
            identity.MainUser = record;
            record.Auth = JsonDocument.Parse(user.Auth.ToString());


            await c.SaveChangesAsync();
            return (user,added);
        }

        public async Task<User?> GetUser(string uid)
        {
            var dbContext = await _dbContext.GetDbContextAsync();

            var guid = Guid.Parse(uid);
            return UserRecord.CreateUserFromRecord(await dbContext.Set<UserRecord>().SingleOrDefaultAsync(u => u.Id == guid));
        }

        public async Task<Dictionary<string, User?>> GetUsers(IEnumerable<string> userIds, CancellationToken cancellationToken)
        {

            var dbContext = await _dbContext.GetDbContextAsync();


            try
            {
                var guids = userIds.Select(id => Guid.TryParse(id, out var guid) ? guid : default).Where(guid => guid != Guid.Empty).ToArray();
                var records = await dbContext.Set<UserRecord>().Where(u => guids.Contains(u.Id)).ToListAsync();
                var results = new Dictionary<string, User?>();
                foreach (var id in userIds)
                {
                    results[id] = UserRecord.CreateUserFromRecord(records.FirstOrDefault(r => r.Id == Guid.Parse(id)));
                }
                return results;
            }
            catch (FormatException ex)
            {
                _logger.Log(LogLevel.Error, "users", $"Failed to parse userIds ({string.Join(",", userIds)})", ex);
                throw;
            }
        }

        public async Task<Dictionary<string, User?>> GetUsersByIdentity(string provider, IEnumerable<string> identifiers)
        {
            var c = await _dbContext.GetDbContextAsync();

            var ids = identifiers.ToList();

            var records = await c.Set<UserRecord>().Where(u => u.Identities.Any(i => i.Provider == provider && ids.Contains(i.Identity))).Include(u => u.Identities).ToListAsync();

            var result = new Dictionary<string, User?>();
            foreach (var id in ids)
            {
                var record = records.FirstOrDefault(r => r.Identities.Any(r => r.Identity == id && r.Provider == provider));
                result[id] = UserRecord.CreateUserFromRecord(record);
            }

            return result;
        }

        public async Task<IEnumerable<User>> Query(IEnumerable<KeyValuePair<string, string>> query, int take, int skip, CancellationToken cancellationToken)
        {
            var dbContext = await _dbContext.GetDbContextAsync();

            var mainQuery = dbContext.Set<UserRecord>().AsQueryable().AsNoTracking();


            foreach (var (key, value) in query)
            {
                mainQuery = mainQuery.Where(r => r.UserData.RootElement.GetProperty("{" + key.Replace('.', ',') + "}").ToString() == value);
            }

            var results = await mainQuery.ToListAsync();

            return results.Select(r => UserRecord.CreateUserFromRecord(r));
        }

        public async Task<IEnumerable<User>> QueryUserHandlePrefix(string prefix, int take, int skip)
        {
            var dbContext = await _dbContext.GetDbContextAsync();

            var records = await dbContext.Set<UserRecord>().Where(u => u.UserHandle != null && u.UserHandle.StartsWith(prefix)).Skip(skip).Take(take).ToListAsync();

            return records.Select(r => UserRecord.CreateUserFromRecord(r));
        }

        public async Task<User> RemoveAuthentication(User user, string provider)
        {
            var c = await _dbContext.GetDbContextAsync();

            user.Auth.Remove(provider);
            var userId = Guid.Parse(user.Id);
            var record = c.Set<UserRecord>().Include(u => u.Identities.Where(i => i.Provider == provider)).SingleOrDefault(u => u.Id == userId);

            if (record.Identities.Any())
            {
                c.Set<IdentityRecord>().RemoveRange(record.Identities);
            }

            record.Auth = JsonDocument.Parse(user.Auth.ToString());

            await c.SaveChangesAsync();
            return user;
        }

        public async Task UpdateLastPlatform(string userId, string lastPlatform)
        {
            var guid = Guid.Parse(userId);
            var dbContext = await _dbContext.GetDbContextAsync();
            var record = await dbContext.Set<UserRecord>().SingleOrDefaultAsync(u => u.Id == guid);
            if (record != null)
            {
                record.LastLogin = DateTime.Now;
                record.LastPlatform = lastPlatform;
                await dbContext.SaveChangesAsync();
            }
        }

        public async Task UpdateUserData<T>(string uid, T data)
        {
            var dbContext = await _dbContext.GetDbContextAsync();

            var guid = Guid.Parse(uid);
            var record = await dbContext.Set<UserRecord>().SingleOrDefaultAsync(u => u.Id == guid);
            if (record == null)
            {
                throw new InvalidOperationException($"notfound");
            }
            else
            {
                if (data is JObject jObject)
                {
                    record.UserData = JsonDocument.Parse(jObject.ToString());
                }
                else
                {
                    record.UserData = JsonSerializer.SerializeToDocument(data!);
                }


                await dbContext.SaveChangesAsync();
            }
        }

        public async Task<string?> UpdateUserHandleAsync(string userId, string newHandle, Func<string, string>? userHandleGenerator, CancellationToken cancellationToken)
        {


            var dbContext = await _dbContext.GetDbContextAsync();

            var guid = Guid.Parse(userId);
            var record = await dbContext.Set<UserRecord>().SingleOrDefaultAsync(u => u.Id == guid);

            var userData = JObject.Parse(record.UserData.RootElement.GetRawText()!);
          

            if (userHandleGenerator !=null)
            {
                int tries = 0;
                bool success = false;
                do
                {
                    try
                    {
                        record.UserHandle = userHandleGenerator(newHandle);
                        await dbContext.SaveChangesAsync();
                        success = true;
                    }
                    catch (Exception)
                    {
                        if (tries > 5)
                        {
                            throw;
                        }
                        tries++;

                    }
                }
                while (!success);
            }
            else
            {
                record.UserHandle = newHandle;
                await dbContext.SaveChangesAsync();
            }


            return record.UserHandle;
        }
    }
}
