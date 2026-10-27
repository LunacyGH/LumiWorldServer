-- Giá gốc chợ cho các tài nguyên nhóm thêm sau 002_market: đồ chế biến, lúa mì, bột mì...
-- Lấy đúng baseValue trong ResourceData phía client (Assets/Resources/Crafting/Items, Acs/Data/Resources).
-- Thiếu dòng ở đây thì đăng bán món đó bị từ chối UNKNOWN_RESOURCE.
-- Đã có khóa (admin đã chỉnh) thì giữ nguyên giá đang dùng.
insert into game_config (key, value) values
    ('market.baseValue.resource.bloomfield.wheat', '0'),
    ('market.baseValue.resource.plain.wheat', '10'),
    ('market.baseValue.resource.plain.wheat_flour', '15'),
    ('market.baseValue.resource.leafwood.fallen_bark', '10'),
    ('market.baseValue.resource.bloomfield.dried_dandelion', '12'),
    ('market.baseValue.resource.leafwood.tender_leaves_crispy', '15'),
    ('market.baseValue.resource.leafwood.roasted_acorns', '18'),
    ('market.baseValue.resource.leafwood.dried_raspberries', '20'),
    ('market.baseValue.resource.windheath.dried_mountain_blueberries', '20'),
    ('market.baseValue.resource.bloomfield.nectar_essence', '25'),
    ('market.baseValue.resource.bloomfield.herbal_flower_powder', '30'),
    ('market.baseValue.resource.leafwood.bark_paper', '35'),
    ('market.baseValue.resource.food.dried_wild_berries_mix', '45'),
    ('market.baseValue.resource.leafwood.honey_glazed_acorns', '55'),
    ('market.baseValue.resource.craft.pressed_flower_paper', '65'),
    ('market.baseValue.resource.craft.scented_paper', '65'),
    ('market.baseValue.resource.forest.wild_herbal_syrup', '70'),
    ('market.baseValue.resource.food.wild_berry_nut_cake', '160')
on conflict (key) do nothing;
