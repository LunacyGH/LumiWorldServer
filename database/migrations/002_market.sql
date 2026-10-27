-- Chợ listing (giai đoạn 1). Một lô là một loại tài nguyên, bán cả lô với một giá Coins.
-- Lô còn Active là hàng đang được server giữ; các trạng thái khác là kết thúc.

create table listings (
    id          uuid primary key,
    seller_id   uuid not null references players (id) on delete cascade,
    resource_id text not null,
    amount      int not null check (amount > 0),
    price_coins int not null check (price_coins > 0),
    status      text not null default 'Active' check (status in ('Active', 'Sold', 'Cancelled', 'Expired')),
    created_at  timestamptz not null default now(),
    expires_at  timestamptz not null,
    buyer_id    uuid references players (id) on delete set null,
    sold_at     timestamptz,
    closed_at   timestamptz
);

create index listings_active_by_resource on listings (resource_id, status, expires_at);
create index listings_active_by_seller on listings (seller_id, status);

-- Giá sàn và trần tính từ baseValue của từng loại (trùng ResourceData phía client).
-- baseValue 0 được coi như 1 khi tính giá. Admin đổi được ở game_config.
insert into game_config (key, value) values
    ('market.baseValue.wood', '5'),
    ('market.baseValue.flower', '4'),
    ('market.baseValue.herb', '6'),
    ('market.baseValue.resource.bloomfield.dandelion_flowers', '0'),
    ('market.baseValue.resource.bloomfield.lavender_sprigs', '0'),
    ('market.baseValue.resource.bloomfield.nectar', '0'),
    ('market.baseValue.resource.bloomfield.wild_daisies', '0'),
    ('market.baseValue.resource.leafwood.acorns', '0'),
    ('market.baseValue.resource.leafwood.oak_wood', '0'),
    ('market.baseValue.resource.leafwood.raspberries', '0'),
    ('market.baseValue.resource.leafwood.sticks', '0'),
    ('market.baseValue.resource.leafwood.tender_leaves', '0'),
    ('market.baseValue.resource.windheath.flax_stalks', '0'),
    ('market.baseValue.resource.windheath.foxtail_plumes', '0'),
    ('market.baseValue.resource.windheath.juniper_berries', '0'),
    ('market.baseValue.resource.windheath.lichen', '0'),
    ('market.baseValue.resource.windheath.mountain_blueberries', '0'),
    ('market.baseValue.resource.windheath.rosemary', '0');
