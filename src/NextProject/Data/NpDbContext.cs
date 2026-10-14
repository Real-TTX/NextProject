using System.Linq.Expressions;
using NextProject.Data.Entities;
using NextProject.Services;
using Microsoft.EntityFrameworkCore;

namespace NextProject.Data;

public class NpDbContext(DbContextOptions<NpDbContext> options, ICurrentUser currentUser, TimeProvider clock) : DbContext(options)
{
    public DbSet<User> User => Set<User>();

    public DbSet<UserTotp> UserTotp => Set<UserTotp>();

    public DbSet<UserRecoveryCode> UserRecoveryCode => Set<UserRecoveryCode>();

    public DbSet<UserSession> UserSession => Set<UserSession>();

    public DbSet<Department> Department => Set<Department>();

    public DbSet<Goal> Goal => Set<Goal>();

    public DbSet<KeyResult> KeyResult => Set<KeyResult>();

    public DbSet<KeyResultCheckIn> KeyResultCheckIn => Set<KeyResultCheckIn>();

    public DbSet<Project> Project => Set<Project>();

    public DbSet<ProjectMember> ProjectMember => Set<ProjectMember>();

    public DbSet<ProjectGoal> ProjectGoal => Set<ProjectGoal>();

    public DbSet<ProjectDependency> ProjectDependency => Set<ProjectDependency>();

    public DbSet<Milestone> Milestone => Set<Milestone>();

    public DbSet<Risk> Risk => Set<Risk>();

    public DbSet<Review> Review => Set<Review>();

    public DbSet<Todo> Todo => Set<Todo>();

    public DbSet<TodoContributor> TodoContributor => Set<TodoContributor>();

    public DbSet<TodoChecklistItem> TodoChecklistItem => Set<TodoChecklistItem>();

    public DbSet<TodoComment> TodoComment => Set<TodoComment>();

    public DbSet<TodoAttachment> TodoAttachment => Set<TodoAttachment>();

    public DbSet<TodoDependency> TodoDependency => Set<TodoDependency>();

    public DbSet<StoredFile> StoredFile => Set<StoredFile>();

    public DbSet<HistoryEntry> HistoryEntry => Set<HistoryEntry>();

    public DbSet<ShareLink> ShareLink => Set<ShareLink>();

    public DbSet<ActivityLog> ActivityLog => Set<ActivityLog>();

    public DbSet<BackupTarget> BackupTarget => Set<BackupTarget>();

    public DbSet<BackupPlan> BackupPlan => Set<BackupPlan>();

    public DbSet<BackupRun> BackupRun => Set<BackupRun>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Tables are PascalCase and named like the entity; enums are stored as text; money has two decimals.
        foreach (var entityType in b.Model.GetEntityTypes())
        {
            entityType.SetTableName(entityType.ClrType.Name);

            foreach (var property in entityType.GetProperties())
            {
                var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                // UpdateState is a number on purpose (0 = deleted, 1 = created, 2 = updated); DayOfWeek is one too.
                if (type.IsEnum && type != typeof(DayOfWeek) && type != typeof(UpdateState))
                {
                    property.SetProviderClrType(typeof(string));
                }
                else if (type == typeof(decimal) && property.GetColumnType() is null)
                {
                    property.SetColumnType("numeric(18,2)");
                }
            }

            if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                b.Entity(entityType.ClrType).HasQueryFilter(NotDeleted(entityType.ClrType));
            }
        }

        // A measured value needs more than two decimals (0.5 hours, 12.375 calls).
        b.Entity<KeyResult>().Property(x => x.StartValue).HasColumnType("numeric(18,4)");
        b.Entity<KeyResult>().Property(x => x.TargetValue).HasColumnType("numeric(18,4)");
        b.Entity<KeyResultCheckIn>().Property(x => x.Value).HasColumnType("numeric(18,4)");
        b.Entity<KeyResult>().Ignore(x => x.CurrentValue).Ignore(x => x.Attainment);
        b.Entity<User>().Ignore(x => x.Initials);
        b.Entity<Project>().Ignore(x => x.IsClosed);
        b.Entity<Risk>().Ignore(x => x.IsActive);
        b.Entity<Todo>().Ignore(x => x.IsClosed);

        b.Entity<User>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique().HasFilter("\"UpdateState\" <> 0");
            e.HasOne(x => x.Totp).WithOne(x => x.User).HasForeignKey<UserTotp>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<UserTotp>().HasIndex(x => x.UserId).IsUnique();
        b.Entity<UserRecoveryCode>().HasOne(x => x.UserTotp).WithMany(x => x.RecoveryCodes).HasForeignKey(x => x.UserTotpId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<UserSession>(e =>
        {
            e.HasIndex(x => x.Token).IsUnique();
            e.HasIndex(x => x.UserId);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Department>().HasIndex(x => x.Name);

        b.Entity<Goal>(e =>
        {
            e.HasIndex(x => x.Year);
            e.HasOne(x => x.OwnerUser).WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<KeyResult>(e =>
        {
            e.HasIndex(x => x.GoalId);
            e.HasIndex(x => x.ProjectId);
            e.HasOne(x => x.Goal).WithMany(x => x.KeyResults).HasForeignKey(x => x.GoalId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Project).WithMany(x => x.KeyResults).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.OwnerUser).WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<KeyResultCheckIn>(e =>
        {
            e.HasIndex(x => new { x.KeyResultId, x.CheckInDate });
            e.HasOne(x => x.KeyResult).WithMany(x => x.CheckIns).HasForeignKey(x => x.KeyResultId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Project>(e =>
        {
            e.HasIndex(x => x.Status);
            e.HasOne(x => x.OwnerUser).WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<ProjectMember>(e =>
        {
            e.HasIndex(x => new { x.ProjectId, x.UserId }).IsUnique();
            e.HasOne(x => x.Project).WithMany(x => x.Members).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<ProjectGoal>(e =>
        {
            e.HasIndex(x => new { x.ProjectId, x.GoalId }).IsUnique();
            e.HasOne(x => x.Project).WithMany(x => x.Goals).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Goal).WithMany(x => x.Projects).HasForeignKey(x => x.GoalId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<ProjectDependency>(e =>
        {
            e.HasIndex(x => new { x.ProjectId, x.DependsOnProjectId }).IsUnique();
            e.HasOne(x => x.Project).WithMany(x => x.Dependencies).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.DependsOnProject).WithMany().HasForeignKey(x => x.DependsOnProjectId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<Milestone>(e =>
        {
            e.HasIndex(x => new { x.ProjectId, x.DueDate });
            e.HasOne(x => x.Project).WithMany(x => x.Milestones).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<Risk>(e =>
        {
            e.HasOne(x => x.Project).WithMany(x => x.Risks).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.OwnerUser).WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<Review>(e =>
        {
            e.HasIndex(x => x.ReviewDate);
            e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Goal).WithMany().HasForeignKey(x => x.GoalId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.CreateUser).WithMany().HasForeignKey(x => x.CreateUserId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Todo>(e =>
        {
            e.HasIndex(x => x.OwnerUserId);
            e.HasIndex(x => x.ProjectId);
            e.HasIndex(x => new { x.Status, x.DueDate });
            e.HasOne(x => x.OwnerUser).WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ReviewerUser).WithMany().HasForeignKey(x => x.ReviewerUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Project).WithMany(x => x.Todos).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Goal).WithMany().HasForeignKey(x => x.GoalId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.KeyResult).WithMany().HasForeignKey(x => x.KeyResultId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Milestone).WithMany().HasForeignKey(x => x.MilestoneId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<TodoContributor>(e =>
        {
            e.HasIndex(x => new { x.TodoId, x.UserId }).IsUnique();
            e.HasOne(x => x.Todo).WithMany(x => x.Contributors).HasForeignKey(x => x.TodoId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<TodoChecklistItem>().HasOne(x => x.Todo).WithMany(x => x.Checklist).HasForeignKey(x => x.TodoId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<TodoComment>(e =>
        {
            e.HasOne(x => x.Todo).WithMany(x => x.Comments).HasForeignKey(x => x.TodoId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.CreateUser).WithMany().HasForeignKey(x => x.CreateUserId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<TodoAttachment>(e =>
        {
            e.HasOne(x => x.Todo).WithMany(x => x.Attachments).HasForeignKey(x => x.TodoId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.StoredFile).WithMany().HasForeignKey(x => x.StoredFileId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<TodoDependency>(e =>
        {
            e.HasIndex(x => new { x.TodoId, x.DependsOnTodoId }).IsUnique();
            e.HasOne(x => x.Todo).WithMany(x => x.Dependencies).HasForeignKey(x => x.TodoId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.DependsOnTodo).WithMany().HasForeignKey(x => x.DependsOnTodoId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<StoredFile>().HasIndex(x => x.Token).IsUnique();
        b.Entity<HistoryEntry>().HasIndex(x => new { x.EntityType, x.EntityId, x.OccurredDate });

        b.Entity<ShareLink>(e =>
        {
            e.HasIndex(x => x.Token).IsUnique();
            e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Goal).WithMany().HasForeignKey(x => x.GoalId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ActivityLog>().HasIndex(x => x.OccurredDate);
        b.Entity<BackupPlan>().HasOne(x => x.Target).WithMany().HasForeignKey(x => x.TargetId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<BackupRun>().HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.SetNull);
    }

    private static LambdaExpression NotDeleted(Type clrType)
    {
        var parameter = Expression.Parameter(clrType, "e");
        var state = Expression.Property(parameter, nameof(ISoftDeletable.UpdateState));
        var body = Expression.NotEqual(state, Expression.Constant(UpdateState.Deleted));
        return Expression.Lambda(body, parameter);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        Stamp();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        Stamp();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>Fills the audit columns and turns a delete of a soft-deletable record into a state change.</summary>
    private void Stamp()
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var userId = currentUser.UserId;

        foreach (var entry in ChangeTracker.Entries<AuditedEntity>().ToList())
        {
            if (entry.State == EntityState.Deleted && entry.Entity is ISoftDeletable soft)
            {
                entry.State = EntityState.Modified;
                soft.UpdateState = UpdateState.Deleted;
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity.CreateDate == default)
                    {
                        entry.Entity.CreateDate = now;
                    }

                    entry.Entity.CreateUserId ??= userId;
                    entry.Entity.UpdateDate = entry.Entity.CreateDate;
                    entry.Entity.UpdateUserId = entry.Entity.CreateUserId;
                    if (entry.Entity is ISoftDeletable added && added.UpdateState == UpdateState.Deleted)
                    {
                        added.UpdateState = UpdateState.Created;
                    }

                    break;

                case EntityState.Modified:
                    entry.Entity.UpdateDate = now;
                    entry.Entity.UpdateUserId = userId;
                    entry.Property(nameof(AuditedEntity.CreateDate)).IsModified = false;
                    entry.Property(nameof(AuditedEntity.CreateUserId)).IsModified = false;
                    if (entry.Entity is ISoftDeletable modified && modified.UpdateState == UpdateState.Created)
                    {
                        modified.UpdateState = UpdateState.Updated;
                    }

                    break;
            }
        }
    }
}
