-- Bản đồ thế giới kiểu Ikariam (giai đoạn 3): mỗi đảo đã chia sẻ có một ô cố định [x:y] trên lưới.
-- Ô được cấp lần đầu người chơi chia sẻ đảo và giữ nguyên sau khi gỡ, để chia sẻ lại vẫn ở chỗ cũ.
-- slot đánh số từ 0; x = slot % 10 + 1, y = slot / 10 + 1 (lưới rộng 10 cột, toạ độ bắt đầu từ 1).

create sequence world_slot_seq minvalue 0 start with 0;

create table world_positions (
    player_id  uuid primary key references players (id) on delete cascade,
    slot       int not null unique,
    x          int not null,
    y          int not null,
    created_at timestamptz not null default now(),
    unique (x, y)
);

-- Đảo đã chia sẻ trước migration này: cấp ô theo thứ tự cập nhật
insert into world_positions (player_id, slot, x, y)
select player_id, rn, rn % 10 + 1, rn / 10 + 1
from (
    select player_id, (row_number() over (order by updated_at, player_id) - 1)::int as rn
    from world_snapshots
) numbered;

select setval('world_slot_seq', coalesce((select max(slot) + 1 from world_positions), 0), false);

-- Tìm theo tên không phân biệt hoa thường
create index players_display_name_lower on players (lower(display_name));
