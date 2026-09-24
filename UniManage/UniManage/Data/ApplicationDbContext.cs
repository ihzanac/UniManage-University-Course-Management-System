using Microsoft.AspNet.Identity.EntityFramework;
using System.Data.Entity;
using UniManage.Models;

namespace UniManage.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext() : base("DefaultConnection", throwIfV1Schema: false)
        {
        }

        public static ApplicationDbContext Create()
        {
            return new ApplicationDbContext();
        }

        public DbSet<CourseCategory> CourseCategories { get; set; }
        public DbSet<Course> Courses { get; set; }
        public DbSet<CourseMaterial> CourseMaterials { get; set; }
        public DbSet<Enrollment> Enrollments { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Assignment> Assignments { get; set; }
        public DbSet<AssignmentSubmission> AssignmentSubmissions { get; set; }
        public DbSet<Grade> Grades { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<MessageNotification> MessageNotifications { get; set; }
        public DbSet<ContactMessage> ContactMessages { get; set; }
        public DbSet<OtpToken> OtpTokens { get; set; }
        public DbSet<SystemUsageLog> SystemUsageLogs { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ApplicationUser>().ToTable("tblUsers");
            // Map Identity2 lockout field to ASP.NET Core Identity column name.
            modelBuilder.Entity<ApplicationUser>()
                .Property(u => u.LockoutEndDateUtc)
                .HasColumnName("LockoutEnd");
            modelBuilder.Entity<IdentityRole>().ToTable("tblRoles");
            modelBuilder.Entity<IdentityUserRole>().ToTable("tblUserRoles");
            modelBuilder.Entity<IdentityUserClaim>().ToTable("tblUserClaims");
            modelBuilder.Entity<IdentityUserLogin>().ToTable("tblUserLogins");

            modelBuilder.Entity<CourseCategory>().ToTable("tblCourseCategory");
            modelBuilder.Entity<Course>().ToTable("tblCourse");
            modelBuilder.Entity<CourseMaterial>().ToTable("tblCourseMaterial");
            modelBuilder.Entity<Enrollment>().ToTable("tblEnrollment");
            modelBuilder.Entity<Payment>().ToTable("tblPayment");
            modelBuilder.Entity<Assignment>().ToTable("tblAssignment");
            modelBuilder.Entity<AssignmentSubmission>().ToTable("tblAssignmentSubmission");
            modelBuilder.Entity<Grade>().ToTable("tblGrade");
            modelBuilder.Entity<Message>().ToTable("tblMessage");
            modelBuilder.Entity<MessageNotification>().ToTable("tblMessageNotification");
            modelBuilder.Entity<ContactMessage>().ToTable("tblContactMessage");
            modelBuilder.Entity<OtpToken>().ToTable("tblOtpToken");
            modelBuilder.Entity<SystemUsageLog>().ToTable("tblSystemUsageLog");

            modelBuilder.Entity<Enrollment>()
                .HasRequired(x => x.Course)
                .WithMany(c => c.Enrollments)
                .HasForeignKey(x => x.CourseId);

            modelBuilder.Entity<Enrollment>()
                .HasRequired(x => x.Student)
                .WithMany(u => u.Enrollments)
                .HasForeignKey(x => x.StudentId)
                .WillCascadeOnDelete(false);

            // Explicitly declare Enrollment as principal and Payment as dependent (1:0..1)
            modelBuilder.Entity<Payment>()
                .HasRequired(p => p.Enrollment)
                .WithOptional(e => e.Payment);

            // Explicitly declare AssignmentSubmission as principal and Grade as dependent (1:0..1)
            modelBuilder.Entity<Grade>()
                .HasRequired(g => g.AssignmentSubmission)
                .WithOptional(s => s.Grade);

            modelBuilder.Entity<Payment>()
                .Property(p => p.Amount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Course>()
                .Property(c => c.Price)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Grade>()
                .Property(g => g.Score)
                .HasPrecision(5, 2);
        }
    }
}
