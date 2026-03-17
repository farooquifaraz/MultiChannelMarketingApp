namespace MarketingApp.Domain.Constants;

public static class AppConstants
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 20;
    public const int BatchSize = 50;
    public const int DelayBetweenBatchesMs = 500;
    public const int MaxContactsPerCampaign = 10000;
    public const int MaxFileUploadSizeMb = 10;
    public const int PasswordMinLength = 8;
    public const int CacheDurationMinutes = 5;
    public const int TemplateCacheDurationMinutes = 10;

    public static class Roles
    {
        public const string Admin = "admin";
        public const string User = "user";
    }

    public static class CacheKeys
    {
        public const string DashboardStats = "dashboard:stats:{0}";
        public const string Templates = "templates:{0}";
    }
}
