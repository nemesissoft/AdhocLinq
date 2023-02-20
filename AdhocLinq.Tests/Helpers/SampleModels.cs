namespace AdhocLinq.Tests.Helpers;

public class User
{
    public Guid Id { get; set; }

    public string UserName { get; set; }

    public int Income { get; set; }

    public UserProfile Profile { get; set; }

    public List<Role> Roles { get; set; }

    public static IList<User> GenerateSampleModels(int total, bool allowNullableProfiles = false)
    {
        if (total < 0) throw new ArgumentOutOfRangeException(nameof(total));

        var list = new List<User>();

        for (int i = 0; i < total; i++)
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                UserName = "User" + i,
                Income = i % 15 * 100
            };

            if (!allowNullableProfiles || i % 8 != 5)
                user.Profile = new UserProfile("FirstName" + i, "LastName" + i, i % 50 + 18);

            user.Roles = new List<Role>(Role.StandardRoles);

            list.Add(user);
        }

        return list.ToArray();
    }
}

public class UserProfile
{
    public string FirstName { get; set; }

    public string LastName { get; set; }

    public int? Age { get; set; }

    public UserProfile(string firstName, string lastName, int? age)
    {
        FirstName = firstName;
        LastName = lastName;
        Age = age;
    }
}

public class Role
{
    public static readonly Role[] StandardRoles = {
        new Role { Name="Admin"},
        new Role { Name="User"},
        new Role { Name="Guest"},
        new Role { Name="G"},
        new Role { Name="J"},
        new Role { Name="A"},
    };

    public Role()
    {
        Id = Guid.NewGuid();
    }

    public Guid Id { get; set; }

    public string Name { get; set; }
}

public class SimpleValuesModel
{
    public float FloatValue { get; set; }

    public decimal DecimalValue { get; set; }
}
