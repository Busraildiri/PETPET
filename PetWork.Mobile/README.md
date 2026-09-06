# Pet'im mobil uygulaması

Bu klasör Expo Go ile açılan mobil uygulamadır. Uygulama, PetWork backend'indeki gerçek
blog, hastalık, tarif ve soru-cevap içeriklerini `/api/mobile/home` üzerinden yükler.

## İlk çalıştırma

Telefon ve bilgisayar aynı Wi-Fi ağına bağlı olmalıdır.

Backend kullanıcı sırlarında `MobileAuth:JwtKey` yoksa bir kez güvenli anahtar oluşturun:

```powershell
$jwtBytes = New-Object byte[] 64
[System.Security.Cryptography.RandomNumberGenerator]::Fill($jwtBytes)
$jwtSecret = [Convert]::ToBase64String($jwtBytes)
dotnet user-secrets set "MobileAuth:JwtKey" $jwtSecret --project .\PetWork\PetWork.csproj
Remove-Variable jwtSecret,jwtBytes
```

1. Visual Studio'da çalışmakta olan PetWork varsa durdurun.
2. Visual Studio üst çubuğundan `mobile-dev` profilini seçip backend'i başlatın.
   Profil görünmüyorsa çözümü kapatıp `PetWork.sln` dosyasını yeniden açın.
3. Visual Studio'da ikinci bir terminal açın ve şunları çalıştırın:

   ```powershell
   cd PetWork.Mobile
   npm install
   npm run start:lan
   ```

4. Telefonda Expo Go'yu açın ve terminaldeki QR kodunu okutun. iPhone'da normal Kamera
   uygulaması da kullanılabilir.

## Bilgisayarın IP adresi değişirse

PowerShell'de `ipconfig` çalıştırın ve Wi-Fi bölümündeki IPv4 adresini bulun. Ardından
`.env.local` içindeki adresi güncelleyin:

```env
EXPO_PUBLIC_API_URL=http://BILGISAYARIN_IP_ADRESI:5147
```

Expo'yu `Ctrl+C` ile durdurup `npm run start:lan -- --clear` komutuyla yeniden başlatın.

Telefon içerikleri yükleyemiyorsa Windows Güvenlik Duvarı sorusunda Node.js ve PetWork
için **Özel ağlara** izin verildiğini kontrol edin. Mobil geliştirme profili yalnızca
yerel geliştirmede HTTP kullanır; normal web profillerinin HTTPS davranışını değiştirmez.
