# Yayın öncesi güvenlik kontrolü

31 Ekim 2026 tarihinde gönderilecek kaynak dosyalar, konuşma dökümü, PDF metni ve mevcut Git geçmişi kontrol edildi. Yayın anındaki içerikte tanınan API anahtarı, sağlayıcı token'ı, JWT, kimlik bilgisi içeren URL veya özel anahtar eşleşmesi bulunmadı. Özel .env, sertifika ve anahtar dosyaları gönderilecek dosyalar arasında yoktu. Bu tarama bütün olası sır biçimlerinin bulunacağı garantisi değildir.

.env.example, README.md ve compose.yaml içindeki SQL parolası yalnız belgelenmiş yerel demo içindir. Kullanıcının gerçek API anahtarı veya hesap parolası değildir. .env ve .env.* Git ve Docker build bağlamından dışlanır; .env.example korunur. Uygulama portları yalnız 127.0.0.1 adresine açılır.

NuGet taramasında altı projede kullanılan doğrudan ve geçişli paketler için mevcut kaynaklarda açık bildirilmedi. Tarama çıktısı pre-delivery-dependencies.txt içindedir. Bu çalışma bir sızma testi veya üretim güvenliği sertifikasyonu değildir.

İki case'in teknik gereksinimleri SON_TESLIM_KONTROLU.md içinde karşılaştırılmıştır. Yerel ve Docker testlerinde 54/54 sonuç alındı; API iş kuralları bu yayın hazırlığında değiştirilmedi. Private GitHub reposuna değerlendirici erişimi ayrıca verilmelidir; alternatif olarak kaynak ZIP'i teslim edilebilir.
