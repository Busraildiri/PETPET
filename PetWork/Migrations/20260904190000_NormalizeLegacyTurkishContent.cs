using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PetWork.Data;

#nullable disable

namespace PetWork.Migrations;

[DbContext(typeof(PetWorkDbContext))]
[Migration("20260904190000_NormalizeLegacyTurkishContent")]
public sealed class NormalizeLegacyTurkishContent : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("UPDATE Questions SET Content = N\u00273 yaşındaki kedim son zamanlarda çok fazla tüy dökmeye başladı. Ne önerirsiniz?\u0027 WHERE Id = 1;");
        migrationBuilder.Sql("UPDATE Questions SET Title = N\u0027Köpeğimin mamasını değiştirmek istiyorum, önerileriniz nedir?\u0027, Content = N\u0027Golden Retriever köpeğim için sağlıklı mama arıyorum.\u0027 WHERE Id = 2;");
        migrationBuilder.Sql("UPDATE Questions SET Title = N\u0027Kuşum tüylerini yoluyor, ne yapmalıyım?\u0027, Content = N\u0027Muhabbet kuşum tüylerini yoluyor, psikolojik olabilir mi?\u0027, Category = N\u0027Kuş\u0027 WHERE Id = 3;");
        migrationBuilder.Sql("UPDATE Answers SET Content = N\u0027Düzenli tarama ve omega-3 takviyesi işe yarayabilir.\u0027 WHERE Id = 1;");
        migrationBuilder.Sql("UPDATE Answers SET Content = N\u0027Mama değişikliği için veterinerinize danışın.\u0027 WHERE Id = 2;");
        migrationBuilder.Sql("UPDATE Recipes SET Title = N\u0027Ev Yapımı Kedi Maması\u0027, Description = N\u0027Kediler için protein açısından zengin mama.\u0027 WHERE Id = 3;");
        migrationBuilder.Sql("UPDATE Recipes SET Title = N\u0027Köpekler İçin Protein Topu\u0027, Description = N\u0027Köpekler için yüksek proteinli atıştırmalık.\u0027 WHERE Id = 4;");
        migrationBuilder.Sql("UPDATE Recipes SET Title = N\u0027Kuşlar İçin Vitamin Karışımı\u0027, Description = N\u0027Kuşlar için vitaminli tohum karışımı.\u0027, PetType = N\u0027Kuş\u0027, AnimalType = N\u0027Kuş\u0027 WHERE Id = 5;");
        migrationBuilder.Sql("UPDATE Guides SET Title = N\u0027Evde Kedi Bakımı Rehberi\u0027 WHERE Id = 1;");
        migrationBuilder.Sql("UPDATE Guides SET Title = N\u0027Köpek Eğitimi: Temel Komutlar\u0027 WHERE Id = 2;");
        migrationBuilder.Sql("UPDATE Guides SET Title = N\u0027Kuş Kafesi Düzenleme\u0027 WHERE Id = 3;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("UPDATE Questions SET Content = N\u00273 yasindaki kedim son zamanlarda çok fazla tüy dökmeye basladi. Ne önerirsiniz?\u0027 WHERE Id = 1;");
        migrationBuilder.Sql("UPDATE Questions SET Title = N\u0027Köpegimin mamasini degistirmek istiyorum, önerileriniz nedir?\u0027, Content = N\u0027Golden retriever köpegim için saglikli mama ariyorum.\u0027 WHERE Id = 2;");
        migrationBuilder.Sql("UPDATE Questions SET Title = N\u0027Kusum tüylerini yoluyor, ne yapmaliyim?\u0027, Content = N\u0027Muhabbet kusum tüylerini yoluyor, psikolojik olabilir mi?\u0027, Category = N\u0027Kus\u0027 WHERE Id = 3;");
    }
}
