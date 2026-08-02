# 생성한 FBX를 한 장에 모아 렌더한다.
#
# 실행:
#   blender -b -P tools/gen/contact.py
#
# 임시 모델이라도 실루엣과 상대 크기는 눈으로 봐야 판단이 선다.
# 사람 키 1.8m 기준 막대를 함께 세워 스케일을 가늠할 수 있게 했다.

import bpy
import math
import os

GEN = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", "..", "assets", "generated"))
OUT = os.path.join(GEN, "contact_sheet.png")

# 배치 순서: 식물군 → 동물군
ORDER = [
    "eye", "ball", "wing", "fruitcrab",
    "debris_a", "debris_b", "debris_c",
    "loosescrap_a", "loosescrap_b",
]

COLS = 5
SPACING = 1.3
HUMAN_H = 1.8


def wipe():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def mat(name, rgb, emit=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.75
    if emit > 0.0:
        bsdf.inputs["Emission Color"].default_value = (*rgb, 1.0)
        bsdf.inputs["Emission Strength"].default_value = emit
    return m


def main():
    wipe()

    # 재질: 발광 식물은 빛나게, 기계는 금속 회색
    plant = mat("plant", (0.35, 0.85, 0.68), emit=1.6)
    fern = mat("fern", (0.55, 0.58, 0.52))
    moss = mat("moss", (0.30, 0.70, 0.62), emit=0.8)
    machine = mat("machine", (0.52, 0.54, 0.58))
    ruler = mat("ruler", (0.9, 0.35, 0.25))

    def pick(name):
        if name.startswith("debris") or name.startswith("loosescrap"):
            return fern      # 잔해는 탁한 회갈색으로 구분
        return machine

    placed = []
    for i, name in enumerate(ORDER):
        path = os.path.join(GEN, name + ".fbx")
        if not os.path.exists(path):
            print("  건너뜀 (없음): " + name)
            continue

        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=path)
        new = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
        if not new:
            continue

        col = i % COLS
        row = i // COLS
        x = (col - (COLS - 1) / 2.0) * SPACING
        y = -row * SPACING

        # 최저점을 바닥에 맞춘다. 부양체·구형이 반쯤 파묻혀 보이는 것을 막는다.
        lowest = min((o.matrix_world @ v.co).z for o in new for v in o.data.vertices)
        for o in new:
            o.location.x += x
            o.location.y += y
            o.location.z -= lowest
            o.data.materials.clear()
            o.data.materials.append(pick(name))
        placed.append((name, x, y))

    # 사람 키 기준 막대
    bpy.ops.mesh.primitive_cylinder_add(radius=0.045, depth=HUMAN_H,
                                        location=(-(COLS / 2.0 + 0.9) * SPACING, 0, HUMAN_H / 2))
    bar = bpy.context.object
    bar.name = "scale_ref_1m8"
    bar.data.materials.append(ruler)

    # 바닥
    bpy.ops.mesh.primitive_plane_add(size=40, location=(0, 0, 0))
    bpy.context.object.data.materials.append(mat("ground", (0.10, 0.10, 0.12)))

    # 조명
    bpy.ops.object.light_add(type="AREA", location=(3.0, -4.0, 5.0))
    key = bpy.context.object
    key.data.energy = 900
    key.data.size = 8

    bpy.ops.object.light_add(type="AREA", location=(-4.0, 3.0, 3.0))
    fill = bpy.context.object
    fill.data.energy = 250
    fill.data.size = 10

    # 카메라: 살짝 위에서 비스듬히
    rows = (len(ORDER) + COLS - 1) // COLS
    cx, cy = 0.0, -(rows - 1) * SPACING / 2.0
    # 격자 전체 + 기준 막대가 들어오도록 거리를 격자 크기에서 계산한다
    span = max(COLS * SPACING, rows * SPACING) + 2.2
    bpy.ops.object.camera_add(location=(cx, cy - span * 1.05, span * 0.62))
    cam = bpy.context.object
    cam.rotation_euler = (math.radians(62), 0, 0)
    cam.data.lens = 35
    bpy.context.scene.camera = cam

    sc = bpy.context.scene
    # 헤드리스(-b)에서는 EEVEE가 GPU 컨텍스트를 요구해 실패한다. Cycles CPU로 굽는다.
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = 48
    sc.cycles.use_denoising = True
    sc.render.resolution_x = 1600
    sc.render.resolution_y = 900
    sc.render.film_transparent = False
    sc.render.filepath = OUT
    bpy.ops.render.render(write_still=True)

    print("")
    print("컨택트 시트: " + OUT)
    print("배치한 모델 %d개" % len(placed))


if __name__ == "__main__":
    main()
