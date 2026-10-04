# Иконка декорации «Табличка» (decor_custom_sign). Модель таблички в игре собирается кодом
# (SignView.cs) по содержимому, поэтому в манифест декора она не входит — здесь только иконка
# в стиле остальных декораций (тот же рендер, что в wi_decor.py).
# Запуск: python wi_sign_icon.py "<проект>"
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from wi_lib import Model, render
import wi_decor


def sign_model():
    m = Model()
    w, h, bottom = 1.5, 0.85, 0.95
    fw = 0.04
    cy = bottom + h / 2
    for s in (-1, 1):
        px = s * (w / 2 + fw + 0.045)
        m.box('Body', (px, (bottom + h + 0.1) / 2, 0), (0.08, bottom + h + 0.1, 0.08), 'wi_base')
        m.box('Body', (px, 0.04, 0), (0.18, 0.08, 0.18), 'wi_base')
        m.box('Body', (px, bottom + h + 0.115, 0), (0.11, 0.03, 0.11), 'wi_gold')
    m.box('Body', (0, cy, 0), (w, h, 0.06), 'wi_wood')
    m.box('Body', (0, cy + h / 2 + fw / 2, 0), (w + fw * 2, fw, 0.08), 'wi_gold')
    m.box('Body', (0, cy - h / 2 - fw / 2, 0), (w + fw * 2, fw, 0.08), 'wi_gold')
    m.box('Body', (-w / 2 - fw / 2, cy, 0), (fw, h, 0.08), 'wi_gold')
    m.box('Body', (w / 2 + fw / 2, cy, 0), (fw, h, 0.08), 'wi_gold')
    # «буквы»: крупная строка, мелкая строка и ромб (рендер смотрит спереди, +X — слева на картинке)
    m.box('Body', (0.08, cy + 0.14, 0.035), (0.9, 0.16, 0.012), 'wi_snow')
    m.box('Body', (0.16, cy - 0.12, 0.035), (0.62, 0.08, 0.012), 'wi_paving')
    m.box('Body', (-0.5, cy - 0.12, 0.035), (0.16, 0.16, 0.012), 'wi_redpaint', rz=45)
    return m


def install(root):
    icons = os.path.join(root, 'Assets', 'Resources', 'Decor', 'Icons')
    path = os.path.join(icons, 'decor_custom_sign.png')
    render([(sign_model(), (0, 0, 0), 0)], path, size=256, yaw=145, pitch=28)
    wi_decor.ensure_meta(path, wi_decor.SPRITE)
    print('icon:', path)


if __name__ == '__main__':
    install(sys.argv[1])
