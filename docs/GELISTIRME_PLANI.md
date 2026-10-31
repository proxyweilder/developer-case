# Geliştirme öncelikleri

Mevcut temel: birbirinden bağımsız iki API ve veritabanı; 54 başarılı otomatik test; çalışır Docker Compose ve örnek HTTP istekleri. 31 Ekim incelemesinde ters sıralı çok ürünlü yarış, tekrar gönderim ve bozuk JSON kontrolleri ile bir sayfalık karar baskısı tamamlandı. Sonuçlar SON_TESLIM_KONTROLU.md içindedir. Aşağıdaki maddeler gelecekte yapılabilecek işlerdir.

## Sıradaki çalışma alanları

1. Okunabilirlik: proje dosyaları ve yapılandırmaları düzenlemek, işlem sırasını açıklayan kısa yorumları korumak; gereksiz katman veya soyutlama eklememek.
2. SQL ve paralellik: örtüşen çok ürünlü siparişler ve daha yoğun yarış senaryolarında sonuç, stok bütünlüğü ve rollback davranışını ölçmek. Doğruluk testiyle yük ölçümünü ayrı raporlamak.
3. Kullanım: beklenen iş kuralı retlerini beklenmeyen sunucu hatalarından loglarda ayırmak; hata yanıtları ve örnek isteklerin tutarlılığını korumak.
4. Teslim: her yeni değişiklikte karar baskısını, başlangıç adımlarını ve kaynak paketini güncel tutmak; commit/push ve değerlendirici erişimini tamamlamak.

Bu maddeler planlanan işlerdir; tamamlandıkları varsayılmamalıdır. Her değişiklikten sonra ilgili testler çalıştırılmalı. Commit ve push yalnız kullanıcı kararıyla yapılır.

## Zaman bilgisi

Kullanıcı projeyi 25 Ekim 2026'da teslim aldığını belirtti. Bu tarihten hesaplanan bir haftalık teslim penceresi 1 Kasım 2026'da biter; mailin kesin saati ayrıca teyit edilir. Dokümandaki en fazla üç saat toplam çalışma koşulu, teslim penceresinden ayrıdır. Genişletilmiş çalışma planı bu koşulun kaldırıldığı anlamına gelmez; gerçek efor kaydı tutulmalıdır.
