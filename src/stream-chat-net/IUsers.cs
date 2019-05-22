using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StreamChat
{
    public interface IUsers
    {
        Task<IEnumerable<User>> UpdateMany(IEnumerable<User> users);
        Task<User> Update(User user);

        Task<User> Delete(string id, bool markMessagesDeleted = false, bool hardDelete = false);
        Task<User> Deactivate(string id, bool markMessagesDeleted = false);

        Task<ExportedUser> Export(string id);

        Task Ban(string targetUserID, string reason, int timeoutMinutes = 0);
        Task Ban(string targetUserID, string reason, Channel channel, int timeoutMinutes = 0);
        Task Unban(string targetUserID, Channel channel = null);

        Task<MuteResponse> Mute(string targetID, string id);
        Task Unmute(string targetID, string id);

        Task MarkAllRead(string id);

        Task<IEnumerable<User>> Query(QueryUserOptions opts);

    }
}