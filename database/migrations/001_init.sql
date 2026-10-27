-- Giai đoạn 0 và lưu world: tài khoản, save duy nhất của mỗi người chơi,
-- snapshot cho người khác thăm, hộp thư, op key, tham số game và log.
-- Chợ (listings) và obstacle thêm ở các file migration sau.

create table players (
    id                  uuid primary key,
    username            text not null,
    -- Tên viết thường để "Lumi" và "lumi" không đăng ký được hai lần
    username_normalized text not null unique,
    display_name        text not null,
    password_hash       text not null,
    role                text not null default 'player' check (role in ('player', 'admin')),
    is_suspended        boolean not null default false,
    created_at          timestamptz not null default now(),
    last_login_at       timestamptz
);

-- Mỗi người chơi đúng một save: khóa chính là player_id.
-- revision tăng 1 sau mỗi lần ghi; client gửi baseRevision để hai máy không ghi đè nhau.
create table saves (
    player_id  uuid primary key references players (id) on delete cascade,
    revision   bigint not null,
    world      jsonb not null,
    economy    jsonb not null,
    updated_at timestamptz not null default now()
);

-- Bản rút gọn của world mà người khác thấy khi thăm đảo.
create table world_snapshots (
    player_id  uuid primary key references players (id) on delete cascade,
    version    int not null,
    data       jsonb not null,
    score      int not null default 0,
    updated_at timestamptz not null default now()
);

create index world_snapshots_recent on world_snapshots (updated_at desc);
create index world_snapshots_score on world_snapshots (score desc);

-- Mọi thứ server gửi cho người chơi: Coins bán được, hàng mua, hàng trả, obstacle.
create table inbox_items (
    id         uuid primary key,
    player_id  uuid not null references players (id) on delete cascade,
    kind       text not null check (kind in ('System', 'CoinsFromSale', 'ItemsBought', 'ItemsReturned', 'Obstacle')),
    payload    jsonb not null,
    created_at timestamptz not null default now(),
    acked_at   timestamptz
);

create index inbox_items_pending on inbox_items (player_id, created_at) where acked_at is null;

-- Gửi lại cùng Idempotency-Key thì trả đúng kết quả cũ, không làm lại thao tác.
create table op_keys (
    player_id   uuid not null references players (id) on delete cascade,
    op_key      text not null,
    status_code int not null,
    response    jsonb,
    created_at  timestamptz not null default now(),
    primary key (player_id, op_key)
);

-- Tham số admin chỉnh được mà không cần build lại client.
create table game_config (
    key        text primary key,
    value      text not null,
    updated_at timestamptz not null default now(),
    updated_by uuid references players (id) on delete set null
);

insert into game_config (key, value) values
    ('market.feePercent', '5'),
    ('market.maxActiveListings', '5'),
    ('market.listingHours', '72'),
    ('market.minPriceFactor', '0.5'),
    ('market.maxPriceFactor', '3'),
    ('obstacle.allowedKinds', 'Obstacle,Drought'),
    ('obstacle.feeCoins', '20'),
    ('obstacle.dailyLimit', '3'),
    ('obstacle.maxPendingPerWorld', '2'),
    ('obstacle.newPlayerProtectionHours', '72'),
    ('sync.inboxSeconds', '60'),
    ('save.uploadSeconds', '300'),
    ('world.maxTiles', '500');

-- Log cho dashboard và số liệu giữ chân người chơi.
create table action_log (
    id        bigserial primary key,
    player_id uuid references players (id) on delete set null,
    action    text not null,
    details   jsonb,
    at        timestamptz not null default now()
);

create index action_log_at on action_log (at);
create index action_log_player on action_log (player_id, at);
