namespace AccountItERP.Helpers
{
    public static class RoleAccess
    {
        public static bool HasAccess(string? role, params string[] allowedRoles)
        {
            if (string.IsNullOrEmpty(role))
                return false;

            return allowedRoles.Contains(role);
        }
    }
}