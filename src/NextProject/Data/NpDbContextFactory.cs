using NextProject.Data.Entities;
using NextProject.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NextProject.Data;

/// <summary>Lets <c>dotnet ef migrations add</c> create the context without starting the application.</summary>
public sealed class NpDbContextFactory : IDesignTimeDbContextFactory<NpDbContext>
{
    public NpDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<NpDbContext>()
            .UseNpgsql("Host=localhost;Database=nextproject_design;Username=postgres;Password=postgres")
            .Options;
        return new NpDbContext(options, new DesignTimeUser(), TimeProvider.System);
    }

    private sealed class DesignTimeUser : ICurrentUser
    {
        public long? UserId => null;

        public string? Name => null;

        public UserRole? Role => null;

        public bool IsAuthenticated => false;

        public bool IsAdmin => false;

        public string? IpAddress => null;

        public IDisposable RunAs(long? userId, string? name, UserRole role = UserRole.Admin) => new NoOp();

        private sealed class NoOp : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
