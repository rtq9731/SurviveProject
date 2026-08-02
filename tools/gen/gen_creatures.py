# 생존게임 — 기계 생물·잔해 임시 모델 생성기
#
# 실행:
#   blender -b -P tools/gen/gen_creatures.py
#
# 식물·바위·구조물은 Poly Universal Pack과 Cross_Plains로 해결된다.
# 팩에 없는 것만 여기서 만든다 — 기계 생물 4종과 잔해다.
# (팩의 기계형 후보는 문짝 기계장치·간판뿐이고, 곤충은 전부 박제 표본이었다)
#
# 목표는 최종 아트가 아니라 **실루엣**이다. 멀리서 봐도 무엇인지 읽혀야 한다.
# 축: 블렌더 +Z 위 → 유니티 +Y 위. 내보낼 때 굽는다. 전방은 +Y(블렌더) → +Z(유니티).

import bpy
import math
import os

TAU = math.pi * 2.0
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "assets", "generated")

# ---------------------------------------------------------------- 파라미터
# 단위 m. 사람 키 1.8m 기준.

PARAMS = {
    # 분해자 — 도감: "작은 추진기로 공중에 부양하는 드론과 비슷한 형태.
    #                몸체 전방에 부착된 큰 눈이 주 탐지기관"
    # 이전 판은 눈이 몸에 파묻혀 안 보였다. 눈을 몸보다 크게 만든다.
    "eye": dict(
        eye_r=0.20,            # 눈이 주역이다. 몸보다 크다
        body_r=0.13,
        body_back=0.17,        # 눈 뒤로 물러난 거리
        iris_r=0.105, iris_out=0.055,
        lid_t=0.030,           # 눈꺼풀 링 두께
        thrusters=3, thruster_r=0.042, thruster_len=0.15, thruster_out=0.24,
        seg=20,
    ),

    # 분해자 — 도감: "동그란 구 형태. 주로 굴러다니는 모습.
    #                몸체 하부의 구멍으로 죽은 기계의 잔해를 회수"
    "ball": dict(
        r=0.32, bands=2, band_t=0.05, band_grow=1.04,
        intake_r=0.14, intake_depth=0.10,
        plates=6, plate_t=0.035,   # 판을 붙여 기계처럼 보이게
        seg=22,
    ),

    # 소형 생산자 — 도감: "몸통 양측에 달린 커다란 팬으로 이동. 잠자리와 비슷한 형태.
    #                    몸체 하부의 스캔 장치가 탐지기관"
    # 이전 판은 팬이 막대처럼 보였다. 큰 원반으로 만든다.
    "wing": dict(
        body_len=0.42, body_r=0.070,
        fan_r=0.30,            # 몸통보다 확실히 크게
        fan_ring_t=0.030, fan_out=0.22,
        blades=5, blade_w=0.055,
        scanner_w=0.16, scanner_h=0.05,
        tail_len=0.26,
        seg=18,
    ),

    # 소형 생산자 — 도감: "다리가 4개 달린 소형 기계. 앞쪽에서 튀어나온 두개의 눈.
    #                    몸통 아래의 입으로 먹이를 담아"
    # 이전 판이 가장 잘 읽혔다. 비율만 다듬는다.
    "fruitcrab": dict(
        body_x=0.36, body_y=0.28, body_z=0.16,
        shell_rise=0.06,       # 등껍질 융기
        legs=4, leg_r=0.030, leg_len=0.30, leg_spread=0.24,
        eye_r=0.050, eye_stalk=0.13,
        seg=14,
    ),

    # 채집 노드 — 죽은 기계의 잔해. 곡괭이로 캔다
    "debris": dict(
        chunks=5, spread=0.34, chunk=0.26,
        rods=4, rod_r=0.028, rod_len=0.44,
    ),

    # 맨손으로 줍는 작은 잔해
    "loosescrap": dict(
        chunks=3, spread=0.13, chunk=0.13,
        rods=2, rod_r=0.016, rod_len=0.20,
    ),
}


# ---------------------------------------------------------------- 유틸

def wipe():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for block in (bpy.data.meshes, bpy.data.objects):
        for item in list(block):
            if item.users == 0:
                block.remove(item)


def activate(o):
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    return o


def join(objs, name):
    objs = [o for o in objs if o is not None]
    if not objs:
        return None
    if len(objs) == 1:
        objs[0].name = name
        return objs[0]
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = name
    return obj


def box(sx, sy, sz, c=(0, 0, 0), rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=c)
    o = bpy.context.object
    o.scale = (sx, sy, sz)
    o.rotation_euler = rot
    activate(o)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return o


def rod(r, length, c=(0, 0, 0), seg=16, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=length, vertices=seg, location=c)
    o = bpy.context.object
    o.rotation_euler = rot
    activate(o)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    return o


def ball(r, c=(0, 0, 0), seg=16):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=r, segments=seg,
                                         ring_count=max(6, seg // 2), location=c)
    return bpy.context.object


def cone(r1, r2, length, c=(0, 0, 0), seg=16, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cone_add(radius1=r1, radius2=r2, depth=length,
                                    vertices=seg, location=c)
    o = bpy.context.object
    o.rotation_euler = rot
    activate(o)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    return o


def torus(major_r, minor_r, c=(0, 0, 0), seg=20, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(major_radius=major_r, minor_radius=minor_r,
                                     major_segments=seg, minor_segments=8, location=c)
    o = bpy.context.object
    o.rotation_euler = rot
    activate(o)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    return o


def sit_on_ground(obj):
    activate(obj)
    lowest = min((obj.matrix_world @ v.co).z for v in obj.data.vertices)
    obj.location.z -= lowest
    bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)
    return obj


def flat(o):
    activate(o)
    bpy.ops.object.shade_flat()
    return o


# ---------------------------------------------------------------- 생물

def build_eye(p):
    """눈 — 부양 분해자. 눈이 몸보다 커서 멀리서도 '눈'으로 읽힌다."""
    parts = []

    # 몸통은 눈 뒤에 숨는 작은 덩어리
    body = ball(p["body_r"], c=(0, -p["body_back"], 0), seg=p["seg"])
    body.scale = (1.0, 1.15, 0.9)
    activate(body)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    parts.append(body)

    # 눈알 — 주역
    eye = ball(p["eye_r"], seg=p["seg"] + 6)
    eye.scale = (1.0, 0.88, 1.0)
    activate(eye)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    parts.append(eye)

    # 눈꺼풀 링. 눈알 둘레를 감싸 렌즈처럼 보이게 한다
    parts.append(torus(p["eye_r"] * 0.94, p["lid_t"],
                       c=(0, p["eye_r"] * 0.10, 0), seg=p["seg"] + 6,
                       rot=(math.pi / 2, 0, 0)))

    # 홍채 — 전방으로 돌출
    parts.append(rod(p["iris_r"], p["iris_out"],
                     c=(0, p["eye_r"] * 0.80, 0), seg=p["seg"],
                     rot=(math.pi / 2, 0, 0)))
    parts.append(ball(p["iris_r"] * 0.55,
                      c=(0, p["eye_r"] * 0.80 + p["iris_out"] * 0.5, 0), seg=14))

    # 추진기 — 몸통 뒤에서 방사형으로. 실루엣에 다리처럼 걸린다
    for i in range(p["thrusters"]):
        a = TAU * i / p["thrusters"] + math.pi / 2
        cx = math.cos(a) * p["thruster_out"]
        cz = math.sin(a) * p["thruster_out"] * 0.75
        # 노즐
        parts.append(cone(p["thruster_r"], p["thruster_r"] * 0.62, p["thruster_len"],
                          c=(cx, -p["body_back"] - 0.02, cz), seg=12,
                          rot=(math.pi / 2, 0, 0)))
        # 몸통과 잇는 팔
        parts.append(rod(p["thruster_r"] * 0.42, p["thruster_out"] * 0.9,
                         c=(cx * 0.5, -p["body_back"], cz * 0.5), seg=8,
                         rot=(0, math.pi / 2 - a, 0)))

    obj = join(parts, "eye")
    return flat(obj)


def build_ball(p):
    """공 — 구르는 분해자. 하부 흡입구로 잔해를 회수한다."""
    parts = []
    parts.append(ball(p["r"], seg=p["seg"]))

    # 적도 띠 — 구르는 방향을 읽게 한다
    for i in range(p["bands"]):
        t = (i + 1) / (p["bands"] + 1)
        z = -p["r"] * 0.55 + p["r"] * 1.1 * t
        rr = math.sqrt(max(0.0, p["r"] ** 2 - z ** 2)) * p["band_grow"]
        parts.append(rod(rr, p["band_t"], c=(0, 0, z), seg=p["seg"]))

    # 외장 판 — 기계라는 인상을 준다
    for i in range(p["plates"]):
        a = TAU * i / p["plates"]
        parts.append(box(p["r"] * 0.30, p["plate_t"], p["r"] * 0.72,
                         c=(math.cos(a) * p["r"] * 0.92, math.sin(a) * p["r"] * 0.92, 0),
                         rot=(0, 0, a)))

    # 하부 흡입구
    parts.append(cone(p["intake_r"], p["intake_r"] * 0.55, p["intake_depth"],
                      c=(0, 0, -p["r"] * 0.90), seg=16))

    obj = join(parts, "ball")
    return flat(obj)


def build_wing(p):
    """날개 — 잠자리형. 팬이 몸통보다 커야 '난다'로 읽힌다."""
    parts = []

    body = rod(p["body_r"], p["body_len"], seg=p["seg"], rot=(math.pi / 2, 0, 0))
    parts.append(body)
    parts.append(ball(p["body_r"] * 1.30, c=(0, p["body_len"] * 0.48, 0), seg=p["seg"]))

    # 하부 스캔 장치
    parts.append(box(p["scanner_w"], p["scanner_w"] * 1.5, p["scanner_h"],
                     c=(0, p["body_len"] * 0.18, -p["body_r"] * 1.25)))

    # 양측 팬 — 세로로 선 원반
    for side in (-1, 1):
        cx = side * p["fan_out"]
        cy = p["body_len"] * 0.06
        # 팬 테두리 링
        parts.append(torus(p["fan_r"], p["fan_ring_t"], c=(cx, cy, 0),
                           seg=p["seg"] + 8, rot=(0, math.pi / 2, 0)))
        # 허브
        parts.append(rod(p["fan_r"] * 0.16, p["fan_ring_t"] * 2.6, c=(cx, cy, 0),
                         seg=12, rot=(0, math.pi / 2, 0)))
        # 날개깃
        for b in range(p["blades"]):
            a = TAU * b / p["blades"]
            parts.append(box(p["fan_ring_t"] * 0.8, p["fan_r"] * 0.92, p["blade_w"],
                             c=(cx,
                                cy + math.cos(a) * p["fan_r"] * 0.46,
                                math.sin(a) * p["fan_r"] * 0.46),
                             rot=(a, 0, 0)))
        # 몸통과 잇는 팔
        parts.append(rod(p["body_r"] * 0.35, p["fan_out"],
                         c=(cx * 0.5, cy, 0), seg=8, rot=(0, math.pi / 2, 0)))

    # 꼬리
    parts.append(cone(p["body_r"] * 0.85, p["body_r"] * 0.12, p["tail_len"],
                      c=(0, -p["body_len"] * 0.5 - p["tail_len"] * 0.45, 0),
                      seg=12, rot=(math.pi / 2, 0, 0)))

    obj = join(parts, "wing")
    return flat(obj)


def build_fruitcrab(p):
    """열매게 — 다리 4개, 눈자루 2개, 몸통 아래 입."""
    parts = []
    top = p["leg_len"] * 0.62

    body = box(p["body_x"], p["body_y"], p["body_z"], c=(0, 0, top))
    parts.append(body)
    # 등껍질 융기
    sh = ball(p["body_x"] * 0.52, c=(0, 0, top + p["body_z"] * 0.35), seg=p["seg"])
    sh.scale = (1.0, p["body_y"] / p["body_x"], p["shell_rise"] / (p["body_x"] * 0.52))
    activate(sh)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    parts.append(sh)

    # 몸통 아래 입
    parts.append(cone(p["body_x"] * 0.34, p["body_x"] * 0.18, p["body_z"] * 0.75,
                      c=(0, 0, top - p["body_z"] * 0.62), seg=14))

    # 다리 4개
    for i in range(p["legs"]):
        sx = 1 if i % 2 == 0 else -1
        sy = 1 if i < 2 else -1
        hx = sx * p["body_x"] * 0.46
        hy = sy * p["body_y"] * 0.46
        # 허벅지: 바깥·위로
        parts.append(rod(p["leg_r"], p["leg_len"] * 0.58,
                         c=(hx + sx * p["leg_spread"] * 0.45,
                            hy + sy * p["leg_spread"] * 0.30,
                            top + p["leg_len"] * 0.10),
                         seg=p["seg"], rot=(sy * 0.55, -sx * 0.95, 0)))
        # 정강이: 아래로
        parts.append(rod(p["leg_r"] * 0.78, p["leg_len"] * 0.72,
                         c=(hx + sx * p["leg_spread"],
                            hy + sy * p["leg_spread"] * 0.62,
                            p["leg_len"] * 0.30),
                         seg=p["seg"]))
        # 발
        parts.append(ball(p["leg_r"] * 1.15,
                          c=(hx + sx * p["leg_spread"],
                             hy + sy * p["leg_spread"] * 0.62, 0.0), seg=10))

    # 눈자루 2개, 전방(+Y)
    for i in range(2):
        sx = 1 if i == 0 else -1
        ex = sx * p["body_x"] * 0.24
        ey = p["body_y"] * 0.52
        ez = top + p["body_z"] * 0.5
        parts.append(rod(p["eye_r"] * 0.32, p["eye_stalk"],
                         c=(ex, ey + p["eye_stalk"] * 0.18, ez + p["eye_stalk"] * 0.42),
                         seg=10, rot=(-0.55, 0, 0)))
        parts.append(ball(p["eye_r"],
                          c=(ex, ey + p["eye_stalk"] * 0.42, ez + p["eye_stalk"] * 0.82),
                          seg=14))

    obj = join(parts, "fruitcrab")
    return sit_on_ground(flat(obj))


# ---------------------------------------------------------------- 잔해

def build_debris(p, seed=0):
    """죽은 기계의 잔해 더미. 각진 덩어리와 삐져나온 막대로 만든다."""
    parts = []

    def rnd(i, m):
        # 시드에 따라 흩어지되 결정적이어야 한다 (같은 씨앗 = 같은 모양)
        return ((seed * 37 + i * 17) % m) / float(m)

    for i in range(p["chunks"]):
        a = TAU * rnd(i, 11)
        d = p["spread"] * (0.25 + 0.75 * rnd(i + 3, 7))
        s = p["chunk"] * (0.45 + 0.65 * rnd(i + 5, 5))
        parts.append(box(s, s * (0.6 + 0.6 * rnd(i + 7, 5)), s * 0.7,
                         c=(math.cos(a) * d, math.sin(a) * d, s * 0.35),
                         rot=(rnd(i, 5) * 0.7, rnd(i + 1, 5) * 0.7, a)))

    for i in range(p["rods"]):
        a = TAU * rnd(i + 13, 9)
        d = p["spread"] * 0.5
        parts.append(rod(p["rod_r"], p["rod_len"] * (0.6 + 0.6 * rnd(i + 2, 5)),
                         c=(math.cos(a) * d, math.sin(a) * d, p["chunk"] * 0.45),
                         seg=8,
                         rot=(math.pi / 2 - rnd(i, 4) * 0.8, 0, a)))

    obj = join(parts, "debris")
    return sit_on_ground(flat(obj))


# ---------------------------------------------------------------- 출력

def export(obj, name):
    os.makedirs(OUT, exist_ok=True)
    path = os.path.normpath(os.path.join(OUT, name + ".fbx"))
    obj.name = name
    activate(obj)
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"MESH"},
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_NONE",
        use_space_transform=True,
        bake_space_transform=True,
        axis_forward="-Z",
        axis_up="Y",
        mesh_smooth_type="FACE",
        use_mesh_modifiers=True,
        use_triangles=False,
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="COPY",
    )
    obj.select_set(False)
    tri = sum(len(poly.vertices) - 2 for poly in obj.data.polygons)
    return len(obj.data.vertices), tri


def main():
    rows = []
    jobs = [
        ("eye", lambda: build_eye(PARAMS["eye"])),
        ("ball", lambda: build_ball(PARAMS["ball"])),
        ("wing", lambda: build_wing(PARAMS["wing"])),
        ("fruitcrab", lambda: build_fruitcrab(PARAMS["fruitcrab"])),
        ("debris_a", lambda: build_debris(PARAMS["debris"], 0)),
        ("debris_b", lambda: build_debris(PARAMS["debris"], 1)),
        ("debris_c", lambda: build_debris(PARAMS["debris"], 2)),
        ("loosescrap_a", lambda: build_debris(PARAMS["loosescrap"], 3)),
        ("loosescrap_b", lambda: build_debris(PARAMS["loosescrap"], 4)),
    ]
    for name, fn in jobs:
        wipe()
        o = fn()
        rows.append((name,) + export(o, name))

    print("")
    print("=" * 52)
    print("  %-18s %8s %8s" % ("이름", "정점", "삼각형"))
    print("  " + "-" * 38)
    for name, v, t in rows:
        print("  %-18s %8d %8d" % (name, v, t))
    print("=" * 52)
    print("  출력: %s" % os.path.normpath(OUT))


if __name__ == "__main__":
    main()
