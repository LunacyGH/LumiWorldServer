# Test các bước 1–8 và 10a (Multiplayer: lưu, thăm đảo, chợ)

Tất cả các bước đều chưa commit. Code nằm ở nhánh T.Anh (Unity), D:\Lumi (server) và D:\LumiDB (database).

## 0. Chuẩn bị

1. Database đã migrate đủ 001–004. Kiểm tra bằng psql hoặc pgAdmin, database `lumi`:
   `select version from schema_migrations order by 1;` phải ra đủ 4 dòng, tới `004_crafted_resource_prices`.
2. Chạy server: `dotnet run` trong `D:\Lumi\src\Lumi.Api` (hoặc `scripts\share-server.ps1` nếu có người chơi từ máy khác).
   Server không khởi động nếu database chưa có 004.
3. Mở Unity, chờ compile xong. Console không có dòng đỏ.
4. Chuẩn bị 2 tài khoản: **A** (chủ đảo) và **B** (người thăm).
5. File save local nằm ở `%USERPROFILE%\AppData\LocalLow\DefaultCompany\LumiWorld\`:
   `world_<id>.json` và `economy_<id>.json`.
6. "Chờ 10 giây" nghĩa là chờ tới khi Console có dòng "Đã lưu lên server" rồi mới dừng Play.
7. Phím thử thiên tai O/F/C/D/B/R và bảng test F1 chỉ chạy trong Editor.

Kiểm tra save trên server (dùng sau mỗi bước khi cần):

```sql
select p.username, s.revision, s.updated_at,
       s.economy->>'version' as econ_version,
       octet_length(s.world::text) as world_bytes,
       octet_length(s.economy::text) as econ_bytes
from saves s join players p on p.id = s.player_id
order by s.updated_at desc;
```

## Bước 1: Chặn thao tác khi đang thăm đảo

1. Đăng nhập A → vào đảo. Ghi lại Coins và kho.
   Kiểm tra đảo mình vẫn đủ Orders, Shop, Market, Chế biến, Thành tựu, và bấm chăm thú được.
2. World map → Visit một đảo khác.
   - Chỉ thấy Coins, kho, nút Market và khung thăm đảo (Back to map / Home / Listings).
   - Không có Orders, Shop, Chế biến, Thành tựu.
   - Bấm vào cụm hay thú: không hiện bảng chăm sóc, không hiện huy hiệu, không có thú hoang.
3. Back to map → Home. Coins và kho y như trước khi đi.
4. Bản build (tắt Development Build): bấm D và F1 trên đảo thì không có gì xảy ra. Trong Editor hai phím vẫn chạy.
5. Mua ở Listings khi đang thăm: Coins trừ ngay, hàng vào kho khi về đảo mình.

## Bước 2: Harmony, cột mốc, thành tựu

1. A → vào đảo. Ghi lại Coins và Harmony (lần đầu là 29).
2. Hoàn thành một cột mốc → mở Thành tựu → nhận. Coins và Harmony tăng đúng mức thưởng.
3. Chờ 10 giây → dừng Play → Play lại → A → vào đảo.
   - Coins và Harmony vẫn giữ.
   - Cột mốc đó hiện đã nhận; bấm nhận lại thì báo đã nhận trước đó.
4. Mở `economy_<id>.json`: có `totalHarmony` và `claimedMilestoneIds` chứa id vừa nhận.
5. New Game với A → vào đảo: Harmony về 29, các cột mốc chưa nhận.
6. Visit đảo khác: không có bảng Thành tựu, Harmony không đổi.

### Đổi tài khoản trong cùng một lần Play

1. A → vào đảo → nhận một cột mốc, xây thêm vài thứ. Ghi lại Coins và Harmony.
2. Về menu → Log out. Console có dòng "Đổi tài khoản: đã ghi save của tài khoản cũ...".
3. Đăng nhập B → vào đảo: chỉ thấy đảo, Coins, kho, Harmony, cột mốc của B, không còn gì của A.
4. Log out → đăng nhập lại A → vào đảo: mọi thứ của A còn nguyên. File `economy_<idA>.json` không bị ghi số liệu của B.
5. Chạy câu SQL ở mục 0: revision của A và B tăng riêng, không lẫn nhau.

## Bước 3: Bài trên tay

1. Tài khoản mới (hoặc New Game) → vào đảo: thấy bộ bài khởi đầu.
   `economy_<id>.json` có `"starterHandGranted":true` và danh sách `hand`.
2. Đặt hết bài → chờ 10 giây → dừng Play → Play lại → vào đảo.
   Tay trống, thẻ đã đặt vẫn trên đảo, không bị chia lại bài khởi đầu.
3. Shop → mua 1 thẻ → ghi lại Coins → dừng Play → Play lại: thẻ vẫn trên tay, Coins chỉ bị trừ một lần.
4. Nhận cột mốc có thẻ thưởng (Leafwood) → dừng Play → Play lại: thẻ vẫn còn, cột mốc không nhận lại được.
5. Bấm F cho cháy rừng trên ô có thú → thú về tay → dừng Play → Play lại: thú chỉ có trên tay, không còn trên đảo, không thành 2 con.
6. Visit đảo khác: không có bài dưới màn hình. Back → Home: tay bài như trước.
7. Log out → đăng nhập tài khoản khác trong cùng lần Play: thấy tay bài của tài khoản đó.

Lưu ý: các save cũ (trên server hiện có 6 save, đều chưa có `starterHandGranted`) sẽ được chia bộ khởi đầu một lần nữa ở lần vào đảo kế tiếp. Đây là hành vi đã định.

## Bước 4: Điểm combo, mốc điểm, lượt mở rộng, hồi combo

1. Vào đảo, đặt thẻ tới khi đạt một mốc điểm. Ghi lại điểm, số lượt Create, tay bài.
2. Không dùng lượt mở rộng. Chờ 10 giây → dừng Play → Play lại → vào đảo.
   - Điểm như cũ, Create còn đúng số lượt.
   - Không có hiệu ứng nhận lượt, không có thẻ thưởng mới.
3. Đặt thêm để vượt mốc kế tiếp: phần thưởng của mốc đó được trao đúng một lần.
4. Dùng lượt mở rộng đặt một mảng đất → dừng Play → Play lại: lần mở rộng sau vẫn bám đúng cạnh các mảng đã mở.
5. Cho hai thú gặp nhau để có combo: điểm tăng, thú báo đang hồi (ví dụ 5m).
   Dừng Play khoảng 1 phút → Play lại: điểm combo còn, thời gian hồi đã trừ đi lúc tắt game.
6. Visit đảo khác → Back → Home: điểm, lượt mở rộng, hồi combo không đổi.

## Bước 5: Giá chợ cho đồ chế biến (server)

1. Server đang chạy, database có 004.
2. Vào đảo, chế biến ra bột mì (Wheat Flour), hoặc dùng F1 trong Editor để thêm vào kho.
3. Market → Sell → bột mì, số lượng 4, giá 60 → Sell. Lô hiện trong My Listings, không có lỗi UNKNOWN_RESOURCE.
4. Tài khoản khác mua lô đó: người bán được +57 Coins (trừ phí 5%), người mua nhận 4 bột mì.
5. Test tự động: `dotnet test` trong `D:\Lumi` phải qua 43/43.

## Bước 6: Hàng chờ chế biến

1. Vào đảo → Chế biến → bắt đầu một mẻ khoảng 5 phút. Nguyên liệu bị trừ.
2. World map → Home: mẻ vẫn chạy, thời gian vẫn giảm.
3. Dừng Play khoảng 1 phút → Play lại → vào đảo: thời gian còn lại giảm thêm khoảng 1 phút.
4. Dừng Play lâu hơn thời gian mẻ → Play lại: mẻ hiện đã xong. Nhận thì kho tăng đúng một lần.
5. Dừng Play ngay sau khi nhận → Play lại: mẻ không còn, không nhận lại được.
6. New Game hoặc tài khoản mới: hàng chờ trống.
7. Visit đảo khác: không có nút Chế biến. Về đảo mình thì mẻ vẫn còn.

## Bước 7: Habitat, sản xuất, thú

1. Chọn tài nguyên cho các ô của một habitat → chờ 10 giây → dừng Play → Play lại: vẫn chọn đúng và đang sản xuất.
2. Để hàng tích trong habitat, ghi lại số → dừng Play 10 phút → Play lại.
   Số hàng tăng tương đương 10 phút (không vượt trần). Console có "Đã cộng sản lượng cho 10 phút".
3. Bấm thu hoạch rồi tắt Unity ngay bằng Task Manager → mở lại: hàng vào kho đúng một lần, không thu lại được.
4. Để một thú đói (no = 0) → dừng Play → Play lại: vẫn đói.
   Nạp thức ăn → dừng Play → Play lại: ngăn thức ăn vẫn đủ.
5. Chăm sóc một con lên 50/100 → dừng Play → Play lại: vẫn 50.
   Nâng rank → dừng Play → Play lại: rank giữ, Coins chỉ trừ một lần.
6. Đặt tên cho một con → dừng Play → Play lại: tên vẫn còn.
   Share đảo → B visit đảo này: con thú hiện đúng tên đó.
7. Visit đảo khác rồi về: habitat, hàng và thú không đổi.

## Bước 8: Thiên tai

1. Bấm O sinh vật cản → chờ 10 giây → dừng Play khoảng 1 phút → Play lại.
   Vật cản vẫn ở đó, vòng thời gian đã trừ khoảng 1 phút, hết giờ thì tự biến mất.
2. Bấm F cho cháy rừng, để lan vài ô → dừng Play khoảng 1 phút → Play lại.
   Đúng các ô đang cháy và có khói như trước, thời gian giảm khoảng 1 phút, không lan thêm lúc tắt.
3. Để cháy rừng kết thúc: ô tro đen, không có cây. Dừng Play → Play lại: vẫn đen.
4. Thẻ Rain lên ô tro → ô hồi phục → dừng Play → Play lại: ô sạch, không có thẻ ma, đặt thẻ mới được.
5. Bấm D (hạn hán) hoặc B (khô cằn) → dừng Play → Play lại: ô vẫn khô, màu và độ khô giữ nguyên.
6. Visit đảo khác rồi về: thiên tai của mình vẫn như trước.
7. Nếu sau khi nạp mà ô cháy vẫn hiện cây nối giữa các cụm, ghi lại để sửa.

## Bước 10a: Trang trí cố định

1. Vào đảo có vài cụm habitat (có cụm 2 ô và cụm 3 ô cùng loại), chụp màn hình.
2. Tắt game rồi mở lại 3 lần, chụp cùng góc. Cây, đá, cỏ và props mép nối phải giống hệt.
3. A bấm Share → B visit đảo A: trang trí giống ảnh của A.
4. Đặt một thẻ habitat mới cạnh cụm cũ, nhớ hình dạng → mở lại game: trang trí ô mới và mép nối mới giữ nguyên.
5. Cho một ô bị cháy → mở lại game: props trên ô và quanh mép vẫn bị ẩn (đảo của chủ).
6. Có thể lệch nhẹ vài props ở mép nối do tốc độ máy (props nối né props còn đang mọc). Nếu lệch nhiều thì ghi lại.

## Kiểm tra database sau khi test

```sql
-- Save có version kinh tế mới (4) sau khi vào đảo bằng client mới
select economy->>'version' as v, count(*) from saves group by 1;

-- Save đảo đã có các khối mới
select p.username,
       s.world ? 'progress' as has_progress, s.world ? 'habitats' as has_habitats,
       s.world ? 'creatures' as has_creatures, s.world ? 'disasters' as has_disasters,
       s.economy ? 'hand' as has_hand, s.economy ? 'craftingJobs' as has_crafting
from saves s join players p on p.id = s.player_id;

-- Snapshot người thăm: kích thước, giới hạn 100 KB
select p.username, jsonb_array_length(w.data->'tiles') as tiles,
       jsonb_array_length(w.data->'placements') as placements,
       octet_length(w.data::text) as bytes
from world_snapshots w join players p on p.id = w.player_id
order by bytes desc;

-- Lô chợ và hộp thư sau test bước 5
select status, count(*) from listings group by 1;
select kind, count(*), count(*) filter (where acked_at is null) as pending from inbox_items group by 1;
```

## Kết quả rà soát database (20/10/2026)

| Mục | Kết quả | Ghi chú |
|---|---|---|
| Migration | Ổn | `lumi` và `lumi_test` đều có 001–004. SchemaCheck đòi đúng 4 bản này. |
| Bảng, quyền | Ổn | 10 bảng, lumi_app đủ select/insert/update/delete và quyền dùng 2 sequence. |
| Lưu save | Ổn | `saves.world` và `saves.economy` là jsonb. Server chỉ kiểm tra có `version`, mỗi phần tối đa 1 MB, không đọc nội dung, nên các trường mới và economy version 4 không bị từ chối. Save lớn nhất hiện tại khoảng 10 KB; một đảo đầy đủ ước khoảng 300–400 KB, vẫn dưới 1 MB. |
| Giá chợ | Ổn | 36/36 ResourceData của client có giá, và giá khớp đúng baseValue trong asset. |
| Hộp thư | Thiếu (bước 9) | Client đã áp System, CoinsFromSale, ItemsBought, ItemsReturned. Loại Obstacle chưa áp nên nằm chờ trên server; đủ 2 cái chờ thì đảo đó không nhận thêm vật cản. `lumi` hiện không có mục nào chờ. |
| Loại vật cản | Ổn | `obstacle.allowedKinds = Obstacle,Drought`, trùng tên DisasterKind của client. |
| Snapshot thăm đảo | Rủi ro | Chỉ gửi tiles và placements, không có dữ liệu riêng tư. Nhưng mỗi ô khoảng 180 byte, và mỗi placement với các trường mới khoảng 300 byte, nên đảo tầm 220–250 ô sẽ vượt giới hạn 100 KB. `world.maxTiles` cho phép tới 500 ô. Khi đó Share báo "island is too large". |
| Ô cháy khi thăm | Thiếu | Snapshot không có dấu thiên tai, nên người thăm thấy ô cháy vẫn có cây và cỏ. |
| Sao lưu | Chưa làm | `D:\LumiDB\backups` trống. Đây là bước 10c. |
| lumi_report, views | Chưa làm | Chưa có view nào; dashboard để sau. |
| Quyền thừa | Rủi ro nhỏ | lumi_app ghi được cả `schema_migrations`; nên chỉ cho select. |

Đề xuất (chưa áp dụng):

1. Tăng `WorldEndpoints.MaxSnapshotBytes` lên khoảng 300 KB, hoặc cho client bỏ các trường người thăm không cần (objectName, thời gian hồi combo) khỏi snapshot.
2. Thêm danh sách ô cháy (`burned: [{q,r}]`) vào snapshot. Đây là trường thêm mới, server bỏ qua, không cần đổi version.
3. Migration 005: `revoke insert, update, delete on schema_migrations from lumi_app;`.
4. Làm bước 9 để client áp mục Obstacle trong hộp thư.
5. Làm bước 10c (sao lưu) trước buổi demo.
