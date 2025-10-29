using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GdeWebLA09DB.Entities
{
    public class User
    {
        public int USERID { get; set; }              // PK
        public Guid GUID { get; set; }
        public string PASSWORD { get; set; } = "";
        public string FIRSTNAME { get; set; } = "";
        public string LASTNAME { get; set; } = "";
        public string? EMAIL { get; set; }
        public bool ACTIVE { get; set; }
        public DateTime MODIFICATIONDATE { get; set; }

        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
        public ICollection<AuthToken> Tokens { get; set; } = new List<AuthToken>();
    }
}

// Entities/Role.cs
namespace GdeWebLA09DB.Entities
{
    public class Role
    {
        public int ROLEID { get; set; }              // PK
        public string ROLENAME { get; set; } = "";
        public bool VALID { get; set; }
        public int MODIFIER { get; set; }
        public DateTime MODIFICATIONDATE { get; set; }

        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    }
}

// Entities/UserRole.cs
namespace GdeWebLA09DB.Entities
{
    public class UserRole
    {
        public int USERROLES { get; set; }           // PK
        public int USERID { get; set; }
        public int ROLEID { get; set; }
        public int CREATOR { get; set; }
        public DateTime CREATINGDATE { get; set; }

        public User User { get; set; } = default!;
        public Role Role { get; set; } = default!;
    }
}

// Entities/AuthToken.cs
namespace GdeWebLA09DB.Entities
{
    public class AuthToken
    {
        public int TOKENID { get; set; }             // PK
        public int USERID { get; set; }
        public string TOKEN { get; set; } = "";
        public DateTime EXPIRATIONDATE { get; set; }

        public User User { get; set; } = default!;
    }
}

