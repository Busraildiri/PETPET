using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using PetWork.Data;
using PetWork.Models;

namespace PetWork.Services;

public sealed class ExternalContentBootstrapService : BackgroundService
{
    private sealed record ResearchSeed(string PmcId, string EnglishTitle, string TurkishTitle, string Authors,
        DateTime PublishedAt, string TurkishSummary);
    private sealed record CuratedQuestionSeed(string ExternalId, string SourceTitle, string TurkishTitle,
        string TurkishQuestion, string TurkishAnswer, string Category, string Tags, string LicenseCode,
        string LicenseUrl);

    private static readonly CuratedQuestionSeed[] CuratedQuestionAnswers =
    [
        new("5998", "How to approach a dog for the first time?", "Bir köpeğe ilk kez nasıl yaklaşmalıyım?",
            "Tanımadığım bir köpekle ilk karşılaşmada onu korkutmadan ve kendimi riske atmadan nasıl yaklaşmalıyım? Hangi beden dili işaretlerine dikkat etmeliyim?",
            "Önce sahibinden izin isteyin. Köpeğe doğrudan üzerine yürümek yerine hafifçe yan dönün; üzerine eğilmeyin, gözlerine uzun süre dikilmeyin ve ani hareket yapmayın. Elinizi yüzüne uzatmak yerine köpeğin isterse size yaklaşmasına ve koklamasına izin verin. Vücudun donması, başını kaçırma, dudak yalama, geriye çekilme, hırlama veya kuyruğu sıkıştırma rahatsızlık işaretleridir. Bunlardan biri görülürse teması zorlamadan mesafe bırakın. Özellikle çocuklarla yapılan tanışmalar yetişkin gözetiminde olmalıdır.",
            "Köpek", "köpek,davranış,iletişim,güvenlik", "CC BY-SA 3.0", "https://creativecommons.org/licenses/by-sa/3.0/"),
        new("1718", "How can I introduce a cat into a household with dogs?", "Kediyi köpeklerin yaşadığı eve nasıl alıştırabilirim?",
            "Evimizde sakin köpekler var ve bir kedi sahiplenmeyi düşünüyoruz. Tanışma sırasında hayvanların stresini ve olası kovalamayı nasıl azaltabiliriz?",
            "Tanışmayı birkaç güne veya gerektiğinde haftalara yayın. Kediyi önce kapısı kapanabilen, mama, su, tuvalet ve yüksek saklanma alanları bulunan ayrı bir odaya yerleştirin. Yatak veya örtü değiştirerek kokularını birbirlerine tanıtın. İlk görsel karşılaşmaları bir bebek kapısı ya da kontrollü aralık üzerinden yapın; köpek tasmalı ve sakin olmalı, sakin davranış ödüllendirilmelidir. Kedinin kaçabileceği yüksek alanlar daima açık kalsın. Kovalamaya, sabitleyerek bakmaya veya korkuya dönüşen buluşmayı sonlandırıp daha kolay bir aşamaya dönün. Hayvanları güvenli olduklarından emin olana kadar yalnız bırakmayın.",
            "Genel", "kedi,köpek,tanıştırma,davranış", "CC BY-SA 3.0", "https://creativecommons.org/licenses/by-sa/3.0/"),
        new("6399", "Is my dog getting enough walks and eating enough food?", "Köpeğimin yürüyüşü ve yediği mama yeterli mi?",
            "Köpeğimin yürüyüş ve mama düzeninin yeterli olup olmadığını nasıl anlayabilirim? Öğününü bitirmemesi hangi durumda endişe vericidir?",
            "Tek bir doğru yürüyüş veya mama miktarı yoktur; yaş, ırk, sağlık durumu ve enerji düzeyi önemlidir. Günlük mamayı ölçün, ödülleri hesaba katın ve kilosunu düzenli izleyin. Kaburgalar hafifçe hissedilebilmeli ancak uzaktan belirgin görünmemelidir. İştah aniden azaldıysa, kilo kaybı sürüyorsa, kusma, ishal, ağız ağrısı, belirgin halsizlik ya da davranış değişikliği varsa veteriner değerlendirmesi gerekir. Yürüyüşte yalnız süreyi değil koklama, zihinsel uğraş ve köpeğin sonrasındaki rahatlama hâlini de değerlendirin.",
            "Köpek", "köpek,sağlık,beslenme,egzersiz", "CC BY-SA 3.0", "https://creativecommons.org/licenses/by-sa/3.0/"),
        new("9781", "What should I look for in a nutritious dog food?", "Besleyici bir köpek maması seçerken neye bakmalıyım?",
            "Mama paketlerindeki bilgiler kafa karıştırıyor. Köpeğimin yaşına ve ihtiyaçlarına uygun, dengeli bir mama seçerken hangi ölçütleri kullanmalıyım?",
            "Etiket üzerinde mamanın köpeğin yaşam evresine uygun, tam ve dengeli olduğuna ilişkin beslenme yeterliliği beyanını arayın. Üreticinin kalite kontrolü, besleme denemeleri ve ulaşılabilir beslenme uzmanlığı içerik listesindeki tek bir sözcükten daha anlamlı olabilir. Porsiyonu başlangıç noktası kabul edip köpeğin kilo ve vücut kondisyonuna göre veterinerle ayarlayın. Alerji, böbrek hastalığı, sindirim sorunu veya başka bir sağlık durumu varsa rastgele diyet değiştirmek yerine veteriner önerisi alın. Mama geçişini birkaç güne yaymak sindirim sorunlarını azaltabilir.",
            "Köpek", "köpek,mama,beslenme,sağlık", "CC BY-SA 3.0", "https://creativecommons.org/licenses/by-sa/3.0/"),
        new("19052", "How to communicate with a cat?", "Kedimle nasıl daha iyi iletişim kurabilirim?",
            "Kedimin miyavlamalarını ve beden dilini daha iyi anlamak, aynı zamanda ona bazı davranışları öğretmek istiyorum. Nereden başlamalıyım?",
            "Tek bir miyavlamayı sözlük gibi çevirmek yerine sesin çıktığı bağlama, kulaklara, kuyruğa, gözlere ve vücut duruşuna birlikte bakın. İstenen davranışları öğretmek için kısa seanslarda ödül ve tıklayıcı gibi olumlu pekiştirme yöntemleri kullanılabilir. Doğru davranış gerçekleştiği anda ödüllendirmek bağlantıyı kolaylaştırır. Bağırmak, korkutmak veya fiziksel ceza güveni zedeler ve istenmeyen davranışı artırabilir. Davranışta ani bir değişim, saklanma veya alışılmadık seslenme varsa önce sağlık sorunu ihtimalini değerlendirin.",
            "Kedi", "kedi,davranış,iletişim,eğitim", "CC BY-SA 3.0", "https://creativecommons.org/licenses/by-sa/3.0/"),
        new("5617", "Should I worry about my kitten eating litter?", "Yavru kedimin kum yemesi tehlikeli mi?",
            "Yavru kedimi zaman zaman tuvalet kumunu yerken görüyorum. Bu yalnızca merak mı, yoksa sağlık sorununun işareti olabilir mi?",
            "Yavru kediler çevreyi ağızlarıyla keşfedebilir; ancak kum yeme tekrarlıyorsa beslenme eksikliği, kansızlık, sindirim sorunu veya pika gibi nedenler araştırılmalıdır. Özellikle topaklanan kum yutulduğunda bağırsakta sorun oluşturabilir. Geçici olarak veterinerin uygun gördüğü, yutulduğunda daha düşük risk taşıyan bir kum kullanın ve erişimi gözlemleyin. Kusma, karında şişlik, dışkılayamama, iştahsızlık veya halsizlik varsa acil veteriner desteği alın; belirti olmasa bile davranış sürüyorsa muayene planlayın.",
            "Kedi", "kedi,yavru kedi,sağlık,kedi kumu", "CC BY-SA 3.0", "https://creativecommons.org/licenses/by-sa/3.0/"),
        new("29608", "When choosing a cat, how to determine temperament and personality and decide on a good fit?", "Sahipleneceğim kedinin karakterini nasıl değerlendirebilirim?",
            "Sakin bir yaşamım var ve karakteri bana uyacak bir kedi sahiplenmek istiyorum. Barınaktaki kısa bir görüşmede kedinin mizacını nasıl anlayabilirim?",
            "En güvenilir başlangıç, kediyi günler veya haftalardır gözlemleyen barınak görevlileri ya da koruyucu aileyle konuşmaktır. Enerji düzeyi, dokunulma isteği, diğer hayvanlara tepkisi, yalnız kalabilmesi ve oyun biçimi hakkında somut örnekler isteyin. Barınak ortamındaki korku gerçek kişiliği gizleyebileceği için mümkünse birden fazla sakin görüşme yapın. Yavru yerine karakteri daha belirgin yetişkin bir kedi düşünmek eşleşmeyi kolaylaştırabilir. Sağlık geçmişi ve evdeki çocuklar veya hayvanlarla uyumu da kararın parçası olmalıdır.",
            "Kedi", "kedi,sahiplendirme,karakter,davranış", "CC BY-SA 4.0", "https://creativecommons.org/licenses/by-sa/4.0/"),
        new("1144", "How many times a day should I feed a cat?", "Bir kediyi günde kaç kez beslemeliyim?",
            "Kedimin günlük mama miktarını tek seferde vermek yerine öğünlere bölmek istiyorum. Uygun sıklığı nasıl belirleyebilirim?",
            "Önemli olan yalnızca öğün sayısı değil, gün boyunca verilen toplam kaloridir. Birçok kedi doğal davranışına daha yakın olan birkaç küçük öğünden yararlanır; otomatik mama kabı da yardımcı olabilir. Yavru, yaşlı, gebe veya hastalığı bulunan kedilerin ihtiyaçları farklıdır. Paketteki miktarı başlangıç kabul edin, mamayı ölçün ve kilo değişimini takip edin. Sürekli açık mama bazı kedilerde aşırı kilo alımına yol açabilir. Reçeteli mama kullanan veya kilo kaybeden bir kedinin düzeni veterinerle birlikte belirlenmelidir.",
            "Kedi", "kedi,beslenme,mama,öğün", "CC BY-SA 3.0", "https://creativecommons.org/licenses/by-sa/3.0/"),
        new("11286", "How can I take care of a bunny?", "Bir tavşanın temel bakım ihtiyaçları nelerdir?",
            "Ailemize genç bir tavşan katıldı. Beslenme, yaşam alanı, sosyalleşme ve sağlık bakımında hangi temel noktaları bilmeliyiz?",
            "Tavşanın beslenmesinin temelini sürekli erişebileceği kaliteli kuru ot, temiz su ve türe uygun yapraklı yeşillikler oluşturur; pelet miktarı yaşına ve kilosuna göre sınırlanır. Küçük bir kafes kalıcı yaşam alanı değildir: koşabileceği, arka ayakları üzerinde doğrulabileceği ve güvenle saklanabileceği geniş, tavşana göre düzenlenmiş bir alan gerekir. Dişleri sürekli büyüdüğü için güvenli kemirme seçenekleri sunun. Tavşanlar sosyal canlılardır; günlük etkileşim ve uygun eşleştirme önemlidir. Kısırlaştırma, aşılar ve bölgesel hastalık riskleri için tavşan konusunda deneyimli bir veterinere danışın.",
            "Genel", "tavşan,bakım,beslenme,yaşam alanı", "CC BY-SA 3.0", "https://creativecommons.org/licenses/by-sa/3.0/"),
        new("10399", "Do pet birds need to bath in water?", "Evcil kuşların suyla banyo yapması gerekir mi?",
            "Evcil kuşuma banyo olanağı sağlamalı mıyım? Her kuş su kabında yıkanmayı sever mi?",
            "Banyo ihtiyacı ve tercih edilen yöntem kuşun türüne ve bireysel alışkanlığına göre değişir. Bazıları sığ bir kapta yıkanmayı, bazıları ince bir su püskürtmesini veya akan suyu tercih eder. Temiz, katkısız ve çok soğuk olmayan su kullanın; kuşu zorla suya sokmayın. Kabı kaymayacak şekilde yerleştirin, suyu sık değiştirin ve kuş tamamen kuruyana kadar cereyandan koruyun. Kuş korku gösteriyorsa farklı bir yöntemi başka bir gün deneyin; özel deri veya tüy sorunu varsa kuş veterinerinden öneri alın.",
            "Kuş", "kuş,bakım,banyo,tüy sağlığı", "CC BY-SA 3.0", "https://creativecommons.org/licenses/by-sa/3.0/")
    ];

    private static readonly ResearchSeed[] GriefResearchArticles =
    [
        new("PMC7558086", "A National Survey of Companion Animal Owners’ Self-Reported Methods of Coping Following Euthanasia",
            "Ötanazi Sonrası Evcil Hayvan Kaybıyla Baş Etme Yolları",
            "Lori R. Kogan ve çalışma arkadaşları", new DateTime(2020, 7, 10),
            "Bu araştırma, bir evcil hayvana ötanazi uygulanmasının ardından insanların nasıl teselli ve destek aradığını 340 katılımcıyla inceliyor. Katılımcıların çoğu yasını özel olarak yaşadığını; yarıdan fazlası sosyal destek aradığını bildirdi. Daha küçük bir bölüm yeni bir hayvan sahiplendi, inanç veya duaya yöneldi ya da bir destek grubuna katıldı.\n\nÇalışmanın önemli sonucu, evcil hayvan kaybının gerçek ve güçlü bir yas deneyimi olduğudur. Veteriner kliniklerinin kayıp yaşayan kişilere erişilebilir destek kaynakları sunması ve bu yası küçümsemeyen bir iletişim kurması öneriliyor."),
        new("PMC12803462", "No pets allowed: Evidence that prolonged grief disorder can occur following the death of a pet",
            "Evcil Hayvan Kaybından Sonra Uzamış Yas Yaşanabilir mi?",
            "Philip Hyland", new DateTime(2026, 1, 14),
            "Bu çalışma, evcil hayvan ölümünden sonraki yasın bazı insanlarda uzun süreli ve günlük yaşamı belirgin biçimde etkileyen bir yapıya dönüşebildiğini inceliyor. Bulgular, hayvan kaybının ardından görülen belirtilerin insan kaybı sonrasında görülen uzamış yas belirtileriyle önemli benzerlikler taşıyabildiğine işaret ediyor.\n\nAraştırma, kişinin yaşadığı acının ‘sadece bir hayvandı’ denilerek geçersizleştirilmemesi gerektiğini vurguluyor. Yoğun ve kalıcı sıkıntı yaşayan kişilerin profesyonel ruh sağlığı desteğine erişebilmesi önem taşıyor."),
        new("PMC8381718", "Older women’s experiences of companion animal death: impacts on well-being and aging-in-place",
            "Yalnız Yaşayan İleri Yaştaki Kadınlarda Evcil Hayvan Kaybı",
            "Joanne K. Branson ve çalışma arkadaşları", new DateTime(2021, 8, 24),
            "Araştırma, yalnız yaşayan ileri yaştaki kadınların evcil hayvan kaybından sonra yaşadığı değişimleri ele alıyor. Görüşmelerde yoğun yas, gündelik düzenin bozulması, yalnızlığın artması ve uzun vadeli toparlanma çabaları öne çıkıyor. Hatıraları korumak, güvenilen kişilerle konuşmak, yeniden sosyal etkinliklere katılmak ve bazı durumlarda hayvan refahı çalışmalarına gönüllü olmak destekleyici bulunmuş.\n\nÇalışma, toplumun evcil hayvan kaybını meşru bir yas olarak tanımasının ve özellikle yalnız yaşayan ileri yaştaki kişilere duygusal destek sunmasının önemini vurguluyor."),
        new("PMC6912713", "Pet Humanisation and Related Grief: Development and Validation of a Structured Questionnaire Instrument to Evaluate Grief in People Who Have Lost a Companion Dog",
            "Köpek Kaybında İnsan–Hayvan Bağı ve Yasın Değerlendirilmesi",
            "Stefania Uccheddu ve çalışma arkadaşları", new DateTime(2019, 11, 7),
            "Bu çalışma, köpeğini kaybeden kişilerin yasını ve insan–köpek bağını değerlendirmek için çok boyutlu bir ölçüm aracı geliştiriyor. Araştırmada 369 köpek sahibinin yanıtları incelendi. Beklenmedik kayıpların öfke ve suçlulukla; ötanazi kararının ise güçlü bağlanma ve yasla ilişkili olabileceği bildirildi.\n\nSonuçlar, köpeğin aile üyesi olarak görülmesinin kaybın psikolojik etkisini artırabileceğini ve destek yaklaşımının kişinin kurduğu ilişkinin anlamını dikkate alması gerektiğini gösteriyor."),
        new("PMC9264879", "Disenfranchised Guilt—Pet Owners’ Burden",
            "Görünmeyen Suçluluk: Evcil Hayvan Sahiplerinin Taşıdığı Yük",
            "Lori R. Kogan, Cori Bussolari, Jennifer Currin-McCulloch, Wendy Packman ve Phyllis Erdman",
            new DateTime(2022, 6, 30),
            "Bu çalışma, evcil hayvan sahiplerinin bakım, zaman ayırma ve iş–aile dengesi konusunda yaşayabildiği suçluluğu inceliyor. Bulgular, bu duyguların insan aile ilişkilerinde görülen sorumluluk ve yetersizlik duygularına benzer düzeylerde olabileceğini gösteriyor.\n\nYazarlar, evcil hayvanla ilgili suçluluğun çoğu zaman çevre tarafından anlaşılmadığını ve bu yüzden kişinin yükünün görünmez kaldığını belirtiyor. Yargılamayan iletişim, gerçekçi bakım beklentileri ve pratik destek seçenekleri öneriliyor.")
    ];
    private static readonly (string Id, string Type)[] CuratedWikipediaPages =
    [
        ("16176078", ExternalContentTypes.Guide),
        ("18870225", ExternalContentTypes.Guide),
        ("2650522", ExternalContentTypes.Disease),
        ("5160376", ExternalContentTypes.BlogPost),
        ("31903693", ExternalContentTypes.BlogPost),
        ("258700", ExternalContentTypes.BlogPost)
    ];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExternalContentBootstrapService> _logger;
    private static readonly (string Provider, string Query, string Type, int Count)[] ExpansionQueries =
    [
        ("Wikimedia", "tr:\"Kedi feromonu\"", ExternalContentTypes.Guide, 1),
        ("Wikimedia", "tr:\"Köpek\"", ExternalContentTypes.Guide, 1),
        ("Wikimedia", "tr:\"Muhabbet kuşu\"", ExternalContentTypes.Guide, 1),
        ("Wikimedia", "tr:\"Tavşan\"", ExternalContentTypes.Guide, 1),
        ("Wikimedia", "tr:\"Kedi maması\"", ExternalContentTypes.Guide, 1),
        ("Wikimedia", "tr:\"Köpek maması\"", ExternalContentTypes.Guide, 1),
        ("Wikimedia", "tr:\"Evcil hayvan\"", ExternalContentTypes.Guide, 1),
        ("Wikimedia", "tr:\"Veteriner hekimlik\"", ExternalContentTypes.Guide, 1),
        ("Wikimedia", "tr:\"Köpek eğitimi\"", ExternalContentTypes.Guide, 1),
        ("Wikimedia", "tr:\"Kedi davranışı\"", ExternalContentTypes.Guide, 1),
        ("Wikimedia", "tr:kedi hastalıkları", ExternalContentTypes.Disease, 3),
        ("Wikimedia", "tr:köpek hastalıkları", ExternalContentTypes.Disease, 3),
        ("Wikimedia", "tr:kuş hastalıkları", ExternalContentTypes.Disease, 2),
        ("Wikimedia", "tr:tavşan hastalıkları", ExternalContentTypes.Disease, 2),
        ("Wikimedia", "tr:\"Kuduz\"", ExternalContentTypes.Disease, 1),
        ("Wikimedia", "tr:\"Toksoplazmoz\"", ExternalContentTypes.Disease, 1),
        ("Wikimedia", "tr:\"Kedi lösemi virüsü\"", ExternalContentTypes.Disease, 1),
        ("Wikimedia", "tr:\"Leptospiroz\"", ExternalContentTypes.Disease, 1),
        ("Wikimedia", "tr:\"Uyuz\"", ExternalContentTypes.Disease, 1),
        ("Wikimedia", "tr:\"Lyme hastalığı\"", ExternalContentTypes.Disease, 1),
        ("Wikimedia", "tr:hayvan refahı", ExternalContentTypes.BlogPost, 3),
        ("Wikimedia", "tr:hayvan sahiplendirme", ExternalContentTypes.BlogPost, 3),
        ("Wikimedia", "tr:insan hayvan bağı", ExternalContentTypes.BlogPost, 2),
        ("Wikimedia", "tr:evcil hayvan kaybı yas", ExternalContentTypes.BlogPost, 2),
        ("Wikimedia", "tr:\"Hayvan destekli terapi\"", ExternalContentTypes.BlogPost, 1),
        ("Wikimedia", "tr:\"Evcil hayvan\"", ExternalContentTypes.BlogPost, 1),
        ("Wikimedia", "tr:\"Veteriner hekimlik\"", ExternalContentTypes.BlogPost, 1),
        ("Wikimedia", "tr:\"Dünya Hayvanları Koruma Günü\"", ExternalContentTypes.BlogPost, 1),
        ("Wikimedia", "tr:\"Sokak hayvanları\"", ExternalContentTypes.BlogPost, 1),
        ("Wikimedia", "tr:\"Hayvan hakları\"", ExternalContentTypes.BlogPost, 1)
    ];
    private const int StackExchangeQuestionTarget = 50;
    private static readonly (string Query, string? Tags)[] StackExchangeQuestionQueries =
    [
        ("health", "dogs"),
        ("health", "cats"),
        ("behavior", "dogs"),
        ("behavior", "cats"),
        ("feeding", null),
        ("training", "dogs"),
        ("grooming", null),
        ("bird", null),
        ("rabbit", null),
        ("loss", null)
    ];
    public ExternalContentBootstrapService(IServiceScopeFactory scopeFactory, ILogger<ExternalContentBootstrapService> logger)
    { _scopeFactory = scopeFactory; _logger = logger; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PetWorkDbContext>();
            var importer = scope.ServiceProvider.GetRequiredService<IExternalContentImportService>();
            var publisher = scope.ServiceProvider.GetRequiredService<IExternalContentPublishingService>();
            var adminId = await db.Users.Where(x => x.IsAdmin).OrderBy(x => x.Id).Select(x => (int?)x.Id)
                .FirstOrDefaultAsync(stoppingToken);
            if (adminId is null) return;
            await SeedGriefResearchAsync(db, adminId.Value, stoppingToken);
            await SeedCuratedQuestionAnswersAsync(db, adminId.Value, stoppingToken);
            await ImportStackExchangeQuestionsAsync(db, importer, publisher, adminId.Value, stoppingToken);

            var blogsToCategorize = await db.BlogPosts.ToListAsync(stoppingToken);
            foreach (var blog in blogsToCategorize)
                blog.Category = BlogCategoryClassifier.Classify(blog.Title, blog.Content);
            await db.SaveChangesAsync(stoppingToken);

            // Önceki otomatik aramalardan kalmış bariz yanlış eşleşmeleri, yeni ağ
            // çağrılarını beklemeden temizle.
            var badBlogs = await db.BlogPosts.Where(x => x.Title.Trim() == "İnsan").ToListAsync(stoppingToken);
            foreach (var badBlog in badBlogs)
            {
                var source = await db.ExternalContentSources.FirstOrDefaultAsync(x =>
                    x.ContentType == ExternalContentTypes.BlogPost && x.LocalContentId == badBlog.Id, stoppingToken);
                if (source is not null)
                {
                    source.LocalContentId = null;
                    source.ReviewStatus = ExternalContentReviewStatuses.Rejected;
                    source.RejectionReason = "Başlık, evcil hayvan içeriğiyle ilgili değil.";
                }
                db.BlogPosts.Remove(badBlog);
            }
            if (badBlogs.Count > 0) await db.SaveChangesAsync(stoppingToken);
            foreach (var page in CuratedWikipediaPages)
            {
                var exists = await db.ExternalContentSources.AnyAsync(x => x.Provider == "Wikimedia" &&
                    x.ExternalId == page.Id && x.ContentType == page.Type, stoppingToken);
                if (exists) continue;
                var source = await importer.ImportAsync("Wikimedia", page.Id, adminId.Value, stoppingToken, page.Type);
                await publisher.PublishTreeAsync(source.Id, adminId.Value, false, stoppingToken);
            }

            const string recipeId = "133865";
            var recipeExists = await db.ExternalContentSources.AnyAsync(x => x.Provider == "Wikibooks" &&
                x.ExternalId == recipeId && x.ContentType == ExternalContentTypes.Recipe, stoppingToken);
            if (!recipeExists)
            {
                var source = await importer.ImportAsync("Wikibooks", recipeId, adminId.Value, stoppingToken);
                source.TranslatedTitle = "Köpekler İçin Fıstık Ezmeli Bisküvi";
                source.TranslatedText = "Malzemeler:\n- 1 çırpılmış yumurta\n- 120 ml tuzsuz, soğan ve sarımsak içermeyen tavuk veya sebze suyu\n- 60 ml bitkisel yağ\n- 75 g ksilitol içermeyen fıstık ezmesi\n- Yaklaşık 140 g un\n- 60 g kepekli tahıl gevreği\n- 20 g yulaf ezmesi\n\nHazırlanışı:\n1. Fırını 177 °C’ye ısıtın.\n2. Fıstık ezmesini, et suyunu ve yağı yumurtayla pürüzsüz olana kadar karıştırın.\n3. Unu, tahıl gevreğini ve yulafı ayrı bir kapta karıştırın.\n4. Kuru karışımı yumurtalı karışıma ekleyip sert bir hamur oluşturun.\n5. Hamuru yaklaşık 6 mm kalınlığında açıp küçük parçalar kesin.\n6. Hafif yağlanmış tepside yaklaşık 15 dakika pişirin ve tamamen soğutun.\n\nGüvenlik notu: Fıstık ezmesi mutlaka ksilitolsüz; et suyu ise tuzsuz, soğan ve sarımsaksız olmalıdır. Yeni bir gıdayı vermeden önce veterinerinize danışın ve küçük porsiyonla başlayın.";
                source.WasTranslated = true;
                source.WasModified = true;
                source.RiskLevel = ExternalContentRiskLevels.High;
                source.AttributionText = "“Dog Biscuits” — Wikibooks contributors, CC BY-SA 4.0. Türkçeye çevrildi ve evcil hayvan güvenliği için PetWork tarafından uyarılarla uyarlandı.";
                await db.SaveChangesAsync(stoppingToken);
                await publisher.PublishTreeAsync(source.Id, adminId.Value, false, stoppingToken);
            }

            var localizedTitles = new Dictionary<string, string>
            {
                ["16176078"] = "Kedi Sağlığı",
                ["18870225"] = "Köpek Bakımı ve Tımarı",
                ["2650522"] = "Köpeklerde Parvovirüs",
                ["5160376"] = "Evcil Hayvan Kaybı ve Yas",
                ["31903693"] = "İnsan–Köpek Bağı",
                ["258700"] = "Evcil Hayvan Sahiplendirme"
            };
            var curatedSources = await db.ExternalContentSources
                .Where(x => x.Provider == "Wikimedia" && localizedTitles.Keys.Contains(x.ExternalId))
                .ToListAsync(stoppingToken);
            foreach (var source in curatedSources)
            {
                var title = localizedTitles[source.ExternalId];
                source.TranslatedTitle = title;
                if (source.LocalContentId is not int localId) continue;
                if (source.ContentType == ExternalContentTypes.Guide)
                {
                    var guide = await db.Guides.FindAsync([localId], stoppingToken);
                    if (guide is not null) guide.Title = title;
                }
                else if (source.ContentType == ExternalContentTypes.Disease)
                {
                    var disease = await db.Diseases.FindAsync([localId], stoppingToken);
                    if (disease is not null) disease.Name = title;
                }
                else if (source.ContentType == ExternalContentTypes.BlogPost)
                {
                    var blog = await db.BlogPosts.FindAsync([localId], stoppingToken);
                    if (blog is not null) blog.Title = title;
                }
            }

            var recipeSource = await db.ExternalContentSources.SingleOrDefaultAsync(x =>
                x.Provider == "Wikibooks" && x.ExternalId == recipeId && x.ContentType == ExternalContentTypes.Recipe,
                stoppingToken);
            if (recipeSource?.LocalContentId is int recipeLocalId)
            {
                var recipe = await db.Recipes.FindAsync([recipeLocalId], stoppingToken);
                if (recipe is not null)
                {
                    const string preparationMarker = "Hazırlanışı:";
                    var text = recipeSource.TranslatedText ?? recipeSource.OriginalText;
                    var markerIndex = text.IndexOf(preparationMarker, StringComparison.OrdinalIgnoreCase);
                    if (markerIndex > 0)
                    {
                        recipe.Ingredients = text[..markerIndex].Replace("Malzemeler:", "", StringComparison.OrdinalIgnoreCase).Trim();
                        recipe.Instructions = text[(markerIndex + preparationMarker.Length)..].Trim();
                        recipe.Content = recipe.Instructions;
                    }
                }
            }

            var blogImages = new Dictionary<string, string>
            {
                ["5160376"] = "img/petwork-community-hero-our-pets-v2.png",
                ["31903693"] = "img/petwork-community-hero.png",
                ["258700"] = "img/hero-community-v2.png"
            };
            foreach (var source in curatedSources.Where(x => x.ContentType == ExternalContentTypes.BlogPost &&
                         x.LocalContentId.HasValue && blogImages.ContainsKey(x.ExternalId)))
            {
                var blog = await db.BlogPosts.FindAsync([source.LocalContentId!.Value], stoppingToken);
                if (blog is null) continue;
                blog.ImageUrl = blogImages[source.ExternalId];
                blog.FeaturedImage = blog.ImageUrl;
            }

            foreach (var blog in await db.BlogPosts.Where(x => x.ImageUrl == "img/blog-default.jpg" || x.ImageUrl == "")
                         .ToListAsync(stoppingToken))
                blog.ImageUrl = blog.FeaturedImage = "img/hero-community-v2.png";
            foreach (var recipe in await db.Recipes.Where(x => x.ImageUrl == "img/recipe-default.jpg" || x.ImageUrl == "")
                         .ToListAsync(stoppingToken))
                recipe.ImageUrl = recipe.FeaturedImage = "img/hero-recipes-v2.png";
            foreach (var recipe in await db.Recipes.Where(x => x.ImageUrl == "img/recipes/recipe1.jpg" ||
                         x.ImageUrl == "img/recipes/recipe2.jpg" || x.ImageUrl == "img/recipes/recipe3.jpg")
                         .ToListAsync(stoppingToken))
                recipe.ImageUrl = recipe.FeaturedImage = "img/hero-recipes-v2.png";
            foreach (var disease in await db.Diseases.Where(x => x.FeaturedImage == "img/disease-default.jpg" || x.FeaturedImage == "")
                         .ToListAsync(stoppingToken))
                disease.FeaturedImage = "img/hero-health-v2.png";
            await db.SaveChangesAsync(stoppingToken);

            // Bu çalıştırmada otomatik eklenen, başlığı seçilen bölümle açıkça uyuşmayan
            // Türkçe arama sonuçlarını vitrinden kaldır. Kaynak kaydı denetim için korunur.
            var irrelevantSources = await db.ExternalContentSources
                .Where(x => x.Provider == "Wikimedia" && x.LocalContentId != null &&
                            (x.ContentType == ExternalContentTypes.Guide || x.ContentType == ExternalContentTypes.Disease ||
                             x.ContentType == ExternalContentTypes.BlogPost))
                .ToListAsync(stoppingToken);
            foreach (var source in irrelevantSources.Where(x =>
                         !IsCandidateRelevant(x.ContentType, x.TranslatedTitle ?? x.SourceTitle)))
            {
                if (source.ContentType == ExternalContentTypes.Guide)
                {
                    var local = await db.Guides.FindAsync([source.LocalContentId!.Value], stoppingToken);
                    if (local is not null) db.Guides.Remove(local);
                }
                else if (source.ContentType == ExternalContentTypes.Disease)
                {
                    var local = await db.Diseases.FindAsync([source.LocalContentId!.Value], stoppingToken);
                    if (local is not null) db.Diseases.Remove(local);
                }
                else
                {
                    var local = await db.BlogPosts.FindAsync([source.LocalContentId!.Value], stoppingToken);
                    if (local is not null) db.BlogPosts.Remove(local);
                }
                source.LocalContentId = null;
                source.ReviewStatus = ExternalContentReviewStatuses.Rejected;
                source.RejectionReason = "Başlık, otomatik yayınlandığı içerik bölümüyle yeterince ilgili değil.";
            }
            await db.SaveChangesAsync(stoppingToken);

            foreach (var expansion in ExpansionQueries)
            {
                IReadOnlyList<ExternalContentCandidate> candidates;
                try
                {
                    candidates = await importer.SearchAsync(expansion.Provider,
                        new ExternalContentSearchRequest(expansion.Query, PageSize: Math.Min(20, expansion.Count * 4)), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "{Provider} üzerinde {Query} araması atlandı.", expansion.Provider, expansion.Query);
                    continue;
                }

                var candidateIds = candidates.Select(x => x.ExternalId).Distinct().ToList();
                var existingIds = await db.ExternalContentSources
                    .Where(x => x.Provider == expansion.Provider && x.ContentType == expansion.Type &&
                                candidateIds.Contains(x.ExternalId) && x.LocalContentId != null)
                    .Select(x => x.ExternalId)
                    .ToListAsync(stoppingToken);
                var existingIdSet = existingIds.ToHashSet(StringComparer.Ordinal);
                var remaining = Math.Max(0, expansion.Count - existingIdSet.Count);
                if (remaining == 0) continue;

                foreach (var candidate in candidates)
                {
                    if (remaining == 0) break;
                    if (existingIdSet.Contains(candidate.ExternalId)) continue;
                    if (!IsCandidateRelevant(expansion.Type, candidate.Title)) continue;
                    try
                    {
                        // ImportAsync aynı kaynak daha önce yarım kaldıysa çeviriyi yeniden dener;
                        // başarıyla yayımlanmış kayıtlarda ise hash kontrolü sayesinde ucuz bir kontroldür.
                        var source = await importer.ImportAsync(expansion.Provider, candidate.ExternalId,
                            adminId.Value, stoppingToken, expansion.Type);
                        if (source.LocalContentId != null ||
                            await publisher.PublishTreeAsync(source.Id, adminId.Value, false, stoppingToken) > 0)
                        {
                            existingIdSet.Add(candidate.ExternalId);
                            remaining--;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "{Provider} içeriği {ExternalId} atlandı.", expansion.Provider, candidate.ExternalId);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception ex) { _logger.LogError(ex, "Başlangıç rehber ve bilgi içerikleri alınamadı."); }
    }

    private static bool IsCandidateRelevant(string contentType, string title)
    {
        var value = title.ToLowerInvariant();
        var hasAnimal = new[] { "kedi", "köpek", "kuş", "tavşan", "hayvan", "evcil", "veteriner" }
            .Any(value.Contains);

        if (contentType == ExternalContentTypes.Guide)
        {
            var unrelated = new[]
            {
                "keseli tavşan", "tiyatro kedi", "kedi gözü bulutsusu", "paslı kedi", "eek! ciyak kedi"
            };
            return hasAnimal && !unrelated.Any(value.Contains);
        }

        if (contentType == ExternalContentTypes.BlogPost)
        {
            var blogTerms = new[]
            {
                "hayvan", "kedi", "köpek", "kuş", "tavşan", "veteriner", "sahip", "barınak", "bakımevi",
                "terapi", "yas", "refah", "hakları", "sokak", "adopt"
            };
            var unrelatedBlogTerms = new[] { "seksi hayvan" };
            return blogTerms.Any(value.Contains) && !unrelatedBlogTerms.Any(value.Contains);
        }

        if (contentType != ExternalContentTypes.Disease) return true;
        var healthTerms = new[]
        {
            "enfeks", "virüs", "grip", "sendrom", "parvo", "kuduz", "parazit", "mantar",
            "dermatit", "alerji", "hastalığı", "hastalıkları", "miksomatoz", "tümör", "toksoplazmoz",
            "lösemi", "leptospiroz", "uyuz", "lyme"
        };
        if (value.Contains("hamam") || value.Contains("köyü")) return false;
        return healthTerms.Any(value.Contains) &&
               (hasAnimal || value.Contains("sendrom") || value.Contains("grip") || value.Contains("parvo") ||
                value.Contains("kuduz") || value.Contains("miksomatoz") || value.Contains("toksoplazmoz") ||
                value.Contains("leptospiroz") || value.Contains("uyuz") || value.Contains("lyme"));
    }

    private async Task ImportStackExchangeQuestionsAsync(PetWorkDbContext db,
        IExternalContentImportService importer, IExternalContentPublishingService publisher,
        int adminId, CancellationToken cancellationToken)
    {
        var completedIds = (await db.ExternalContentSources
                .Where(x => x.Provider == "StackExchange" &&
                            x.ContentType == ExternalContentTypes.Question &&
                            x.LocalContentId != null &&
                            x.Children.Any(child => child.ContentType == ExternalContentTypes.Answer &&
                                                    child.LocalContentId != null))
                .Select(x => x.ExternalId)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        if (completedIds.Count >= StackExchangeQuestionTarget) return;

        foreach (var (query, tags) in StackExchangeQuestionQueries)
        {
            if (completedIds.Count >= StackExchangeQuestionTarget) break;

            IReadOnlyList<ExternalContentCandidate> candidates;
            try
            {
                candidates = await importer.SearchAsync("StackExchange",
                    new ExternalContentSearchRequest(query, tags, 60), cancellationToken);
            }
            catch (StackExchangeThrottleException ex)
            {
                _logger.LogWarning(ex, "StackExchange kotası dolu; kalan soru-cevap aramaları bu çalıştırmada ertelendi.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "StackExchange üzerinde {Query} araması atlandı.", query);
                continue;
            }

            foreach (var candidate in candidates)
            {
                if (completedIds.Count >= StackExchangeQuestionTarget) break;
                if (completedIds.Contains(candidate.ExternalId)) continue;

                try
                {
                    var source = await importer.ImportAsync("StackExchange", candidate.ExternalId,
                        adminId, cancellationToken, ExternalContentTypes.Question);
                    await publisher.PublishTreeAsync(source.Id, adminId, false, cancellationToken);

                    var hasPublishedPair = await db.ExternalContentSources.AnyAsync(x =>
                        x.Id == source.Id && x.LocalContentId != null &&
                        x.Children.Any(child => child.ContentType == ExternalContentTypes.Answer &&
                                                child.LocalContentId != null), cancellationToken);
                    if (hasPublishedPair) completedIds.Add(candidate.ExternalId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "StackExchange soru-cevap içeriği {ExternalId} atlandı.",
                        candidate.ExternalId);
                }
            }
        }

        _logger.LogInformation("Yayımlanmış StackExchange soru-cevap sayısı: {Count}/{Target}.",
            completedIds.Count, StackExchangeQuestionTarget);
    }

    private static async Task SeedCuratedQuestionAnswersAsync(PetWorkDbContext db, int adminId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        foreach (var seed in CuratedQuestionAnswers)
        {
            var exists = await db.ExternalContentSources.AnyAsync(x =>
                x.Provider == "StackExchange" && x.ExternalId == seed.ExternalId &&
                x.ContentType == ExternalContentTypes.Question, cancellationToken);
            if (exists) continue;

            var question = new Question
            {
                Title = seed.TurkishTitle,
                Content = seed.TurkishQuestion,
                Category = seed.Category,
                Tags = seed.Tags,
                UserId = adminId,
                CreatedDate = now
            };
            db.Questions.Add(question);
            await db.SaveChangesAsync(cancellationToken);

            var answer = new Answer
            {
                Content = seed.TurkishAnswer,
                QuestionId = question.Id,
                UserId = adminId,
                CreatedDate = now
            };
            db.Answers.Add(answer);
            await db.SaveChangesAsync(cancellationToken);

            var sourceUrl = $"https://pets.stackexchange.com/questions/{seed.ExternalId}";
            var rootSource = new ExternalContentSource
            {
                Provider = "StackExchange",
                ExternalId = seed.ExternalId,
                ContentType = ExternalContentTypes.Question,
                LocalContentId = question.Id,
                SourceUrl = sourceUrl,
                ApiUrl = $"https://api.stackexchange.com/2.3/questions/{seed.ExternalId}?site=pets&filter=withbody",
                SourceTitle = seed.SourceTitle,
                SourceAuthorName = "Pets Stack Exchange topluluğu",
                SourceAuthorUrl = sourceUrl,
                SourceLanguage = "en",
                LicenseCode = seed.LicenseCode,
                LicenseUrl = seed.LicenseUrl,
                ImportedAt = now,
                LastCheckedAt = now,
                OriginalContentHash = ContentHash(seed.SourceTitle + seed.TurkishQuestion),
                OriginalText = "Özgün soru metni kaynak bağlantısında yer almaktadır.",
                TranslatedTitle = seed.TurkishTitle,
                TranslatedText = seed.TurkishQuestion,
                Tags = seed.Tags,
                Category = seed.Category,
                TranslationProvider = "PetWorkCurated",
                TranslationVersion = "1",
                TranslatedAt = now,
                WasTranslated = true,
                WasModified = true,
                AttributionText = $"“{seed.SourceTitle}” — Pets Stack Exchange topluluğu, {seed.LicenseCode}. PetWork tarafından Türkçeye çevrilmiş ve kısaltılmıştır.",
                ReviewStatus = ExternalContentReviewStatuses.PublishedUnreviewed,
                RiskLevel = seed.Tags.Contains("sağlık", StringComparison.OrdinalIgnoreCase)
                    ? ExternalContentRiskLevels.Medium
                    : ExternalContentRiskLevels.Low,
                IsSourceAvailable = true
            };
            var answerSource = new ExternalContentSource
            {
                Provider = "StackExchange",
                ExternalId = $"curated-answer-{seed.ExternalId}",
                ContentType = ExternalContentTypes.Answer,
                LocalContentId = answer.Id,
                ParentSource = rootSource,
                SourceUrl = sourceUrl + "#answers",
                ApiUrl = sourceUrl,
                SourceTitle = seed.SourceTitle + " — answer",
                SourceAuthorName = "Pets Stack Exchange topluluğu",
                SourceAuthorUrl = sourceUrl,
                SourceLanguage = "en",
                LicenseCode = seed.LicenseCode,
                LicenseUrl = seed.LicenseUrl,
                ImportedAt = now,
                LastCheckedAt = now,
                OriginalContentHash = ContentHash(seed.SourceTitle + seed.TurkishAnswer),
                OriginalText = "Özgün yanıt metni kaynak bağlantısında yer almaktadır.",
                TranslatedTitle = seed.TurkishTitle + " — yanıt",
                TranslatedText = seed.TurkishAnswer,
                Tags = seed.Tags,
                Category = seed.Category,
                TranslationProvider = "PetWorkCurated",
                TranslationVersion = "1",
                TranslatedAt = now,
                WasTranslated = true,
                WasModified = true,
                AttributionText = $"“{seed.SourceTitle}” yanıtlarından Türkçe özet — Pets Stack Exchange topluluğu, {seed.LicenseCode}.",
                ReviewStatus = ExternalContentReviewStatuses.PublishedUnreviewed,
                RiskLevel = rootSource.RiskLevel,
                IsSourceAvailable = true
            };
            db.ExternalContentSources.AddRange(rootSource, answerSource);
            await db.SaveChangesAsync(cancellationToken);

            db.ContentImportAudits.AddRange(
                new ContentImportAudit
                {
                    ExternalContentSourceId = rootSource.Id,
                    EventType = "CuratedImport",
                    Outcome = "Success",
                    Details = "Kaynaklı soru Türkçe özet olarak yayımlandı.",
                    PerformedByUserId = adminId,
                    CreatedAt = now
                },
                new ContentImportAudit
                {
                    ExternalContentSourceId = answerSource.Id,
                    EventType = "CuratedImport",
                    Outcome = "Success",
                    Details = "Kaynaklı yanıt Türkçe özet olarak yayımlandı.",
                    PerformedByUserId = adminId,
                    CreatedAt = now
                });
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static string ContentHash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static async Task SeedGriefResearchAsync(PetWorkDbContext db, int adminId, CancellationToken cancellationToken)
    {
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        foreach (var article in GriefResearchArticles)
        {
            if (await db.ExternalContentSources.AnyAsync(x => x.Provider == "PubMed Central" &&
                    x.ExternalId == article.PmcId && x.ContentType == ExternalContentTypes.BlogPost, cancellationToken))
                continue;

            var blog = new BlogPost
            {
                Title = article.TurkishTitle,
                Content = article.TurkishSummary,
                Category = "Yas ve Kayıp",
                FeaturedImage = "img/hero-community-v2.png",
                ImageUrl = "img/hero-community-v2.png",
                PublishedDate = now,
                PublishDate = now,
                UserId = adminId
            };
            db.BlogPosts.Add(blog);
            await db.SaveChangesAsync(cancellationToken);

            var sourceUrl = $"https://pmc.ncbi.nlm.nih.gov/articles/{article.PmcId}/";
            var source = new ExternalContentSource
            {
                Provider = "PubMed Central",
                ExternalId = article.PmcId,
                ContentType = ExternalContentTypes.BlogPost,
                LocalContentId = blog.Id,
                SourceUrl = sourceUrl,
                ApiUrl = $"https://www.ebi.ac.uk/europepmc/webservices/rest/{article.PmcId}/fullTextXML",
                SourceTitle = article.EnglishTitle,
                SourceAuthorName = article.Authors,
                SourceAuthorUrl = sourceUrl,
                SourceLanguage = "en",
                LicenseCode = "CC BY 4.0",
                LicenseUrl = "https://creativecommons.org/licenses/by/4.0/",
                OriginalPublishedAt = DateTime.SpecifyKind(article.PublishedAt, DateTimeKind.Unspecified),
                ImportedAt = now,
                LastCheckedAt = now,
                OriginalContentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(article.EnglishTitle))),
                OriginalText = "Açık erişimli araştırma makalesi. Özgün özet ve tam metin kaynak bağlantısında yer almaktadır.",
                TranslatedTitle = article.TurkishTitle,
                TranslatedText = article.TurkishSummary,
                Tags = "evcil hayvan kaybı,yas,araştırma,psikolojik destek",
                Category = "Yas ve Kayıp",
                TranslationProvider = "PetWorkCurated",
                TranslationVersion = "1",
                TranslatedAt = now,
                WasTranslated = true,
                WasModified = true,
                AttributionText = $"“{article.EnglishTitle}” — {article.Authors}, PubMed Central, CC BY 4.0. PetWork tarafından Türkçe araştırma özeti hazırlanmıştır.",
                ReviewStatus = ExternalContentReviewStatuses.PublishedUnreviewed,
                RiskLevel = ExternalContentRiskLevels.Medium,
                IsSourceAvailable = true
            };
            db.ExternalContentSources.Add(source);
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
