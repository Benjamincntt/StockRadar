"""Độ bền cho capvon.py: so trần theo thị trường với trần LUÔN BẬT (chỉ là dùng ít vốn hơn), thêm ngưỡng lân cận,
và vốn 100 triệu."""
import sys
exec(open(sys.argv[1] + '/capvon.py', encoding='utf-8').read().split("\n\nV0 = int(")[0])

for V0 in (40_000, 100_000):
    print(f'\n### Von {V0/1000:.0f} trieu')
    dong('goc (khong tran)', *run_cap(PARTS, V0, 1.0, 0.0, False))
    print('  -- tran LUON BAT (khong xet thi truong)')
    for cap in (0.7, 0.8, 0.9):
        dong(f'luon tran {cap*100:.0f}%', *run_cap(PARTS, V0, cap, 9.0, False))
    print('  -- tran khi chi so duoi MA50 qua nguong, ap moi kich ban')
    for nguong in (-0.03, -0.05, -0.07):
        for cap in (0.7, 0.8, 0.9):
            dong(f'MA50 {nguong*100:+.0f}% tran {cap*100:.0f}%', *run_cap(PARTS, V0, cap, nguong, False))
