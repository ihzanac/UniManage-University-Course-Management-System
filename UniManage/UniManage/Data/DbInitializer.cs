using UniManage;

namespace UniManage.Data
{
    public static class DbInitializer
    {
        public static void Seed()
        {
            RoleSeed.EnsureRolesAndAdmin();
        }
    }
}
