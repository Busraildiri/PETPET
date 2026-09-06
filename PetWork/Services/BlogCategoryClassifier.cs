namespace PetWork.Services;

public static class BlogCategoryClassifier
{
    public static readonly string[] DisplayOrder =
    [
        "Yas ve Kayıp",
        "Hayvan Hakları ve Refahı",
        "Sahiplendirme ve Sokak Hayvanları",
        "İnsan–Hayvan Bağı",
        "Sağlık ve Veterinerlik",
        "Bakım ve Yaşam",
        "Topluluk ve Yaşam"
    ];

    public static string Classify(string? title, string? content)
    {
        // Uzun içeriklerde birçok konu kelimesi birlikte geçebildiği için kategori
        // kararını başlıktan veriyoruz; bu, örneğin veterinerlik yazılarının sırf
        // metinde "ölüm" geçti diye Yas ve Kayıp'a taşınmasını önler.
        var value = (title ?? string.Empty).ToLowerInvariant();
        if (Has(value, "yas", "kayıp", "kaybı", "ölüm", "ötanazi", "suçluluk", "grief", "bereavement", "euthanasia"))
            return "Yas ve Kayıp";
        if (Has(value, "sahiplen", "sokak", "barınak", "bakımevi", "koruyucu aile", "foster", "adopt"))
            return "Sahiplendirme ve Sokak Hayvanları";
        if (Has(value, "hayvan hakk", "hayvan refah", "koruma günü", "bildirge", "animal welfare"))
            return "Hayvan Hakları ve Refahı";
        if (Has(value, "veteriner", "hastalık", "sağlık", "tedavi", "klinik", "medicine"))
            return "Sağlık ve Veterinerlik";
        if (Has(value, "terapi", "insan–", "insan-", "bağı", "arkadaşlık", "companion", "human-animal", "human–animal"))
            return "İnsan–Hayvan Bağı";
        if (Has(value, "bakım", "beslen", "mama", "eğitim", "davranış", "evcil hayvan"))
            return "Bakım ve Yaşam";
        return "Topluluk ve Yaşam";
    }

    private static bool Has(string value, params string[] terms) => terms.Any(value.Contains);
}
