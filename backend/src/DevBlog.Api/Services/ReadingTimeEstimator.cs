namespace DevBlog.Api.Services;

public static class ReadingTimeEstimator
{
    // 200 kelime/dakika varsayimiyla tahmini okuma suresi; mevcut post'lar icin
    // migration'daki backfill SQL'i (AddReadingInMinutesToPost) ile ayni formulu kullanir.
    public static int EstimateMinutes(string content)
    {
        var wordCount = content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return Math.Max(1, (int)Math.Round(wordCount / 200.0));
    }
}
