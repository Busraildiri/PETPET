namespace PetWork.Validation;

public static class ProfileOptionCatalog
{
    public static readonly HashSet<string> Cities = new([
        "Adana", "Adıyaman", "Afyonkarahisar", "Ağrı", "Aksaray", "Amasya", "Ankara", "Antalya", "Ardahan", "Artvin", "Aydın", "Balıkesir", "Bartın", "Batman", "Bayburt", "Bilecik", "Bingöl", "Bitlis", "Bolu", "Burdur", "Bursa", "Çanakkale", "Çankırı", "Çorum", "Denizli", "Diyarbakır", "Düzce", "Edirne", "Elazığ", "Erzincan", "Erzurum", "Eskişehir", "Gaziantep", "Giresun", "Gümüşhane", "Hakkâri", "Hatay", "Iğdır", "Isparta", "İstanbul", "İzmir", "Kahramanmaraş", "Karabük", "Karaman", "Kars", "Kastamonu", "Kayseri", "Kilis", "Kırıkkale", "Kırklareli", "Kırşehir", "Kocaeli", "Konya", "Kütahya", "Malatya", "Manisa", "Mardin", "Mersin", "Muğla", "Muş", "Nevşehir", "Niğde", "Ordu", "Osmaniye", "Rize", "Sakarya", "Samsun", "Siirt", "Sinop", "Sivas", "Şanlıurfa", "Şırnak", "Tekirdağ", "Tokat", "Trabzon", "Tunceli", "Uşak", "Van", "Yalova", "Yozgat", "Zonguldak"
    ], StringComparer.Ordinal);

    public static readonly HashSet<string> Occupations = new([
        "Öğrenci", "Yazılımcı", "Mühendis", "Öğretmen", "Doktor", "Veteriner hekim", "Hemşire", "Psikolog", "Avukat", "Mimar", "Tasarımcı", "Akademisyen", "Muhasebeci", "Satış uzmanı", "Pazarlama uzmanı", "İnsan kaynakları uzmanı", "Kamu çalışanı", "Esnaf", "Serbest meslek", "Ev çalışanı", "Emekli", "Çalışmıyor", "Diğer"
    ], StringComparer.Ordinal);

    public static readonly HashSet<string> LivingSituations = new([
        "Tek başına", "Aileyle", "Partnerle", "Ev arkadaşıyla"
    ], StringComparer.Ordinal);
}
