# 생존게임 — 식물군·생물 임시 모델 생성기
#
# 실행:
#   blender -b -P tools/gen/gen_flora.py
#
# 목적은 최종 아트가 아니라 배치·스케일·실루엣을 확인할 임시 모델이다.
# 전부 파라미터 하나에서 파생되므로, 크기가 어색하면 PARAMS만 고치면 된다.
#
# 축: 블렌더 +Z 위 → 유니티 +Y 위. 내보낼 때 축을 바꿔 굽는다.

import bpy
import bmesh
import math
import os

TAU = math.pi * 2.0
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "assets", "generated")

# ---------------------------------------------------------------- 파라미터
# 단위 m. 사람 키를 1.8m로 보고 잡았다.

PARAMS = {
    # 1차 생산자 — 매크로늄에서 직접 에너지를 얻고 빛을 낸다
    "emberfungus": dict(          # 불씨버섯. 주먹만 하고 군락을 이룬다
        stalk_h=0.16, stalk_r=0.028,
        cap_r=0.11, cap_h=0.075,
        cap_droop=0.35,           # 갓이 아래로 처지는 정도
        gills=9,                  # 갓 아래 주름 수
        seg=20,
    ),
    "mossnode": dict(             # 매크로늄 이끼. 바위에 얇게 깔린다
        radius=0.42, height=0.045, lumps=7, seg=18,
    ),

    # 2차 생산자 — 버섯 빛으로 광합성
    "ashfern": dict(              # 재양치. 잔디 크기
        fronds=6, frond_len=0.42, frond_w=0.075,
        arch=0.55,                # 잎이 휘는 정도
        leaflets=7,
    ),

    # 동물군 — 전부 기계다. 각진 면과 단순한 덩어리로 표현한다
    "eye": dict(                  # 분해자. 부양 드론, 전방에 큰 눈
        body_r=0.17, eye_r=0.115, eye_out=0.10,
        thrusters=3, thruster_r=0.035, thruster_len=0.11, thruster_out=0.15,
        seg=20,
    ),
    "ball": dict(                 # 분해자. 구르며 잔해를 회수
        r=0.34, bands=3, band_t=0.045, intake_r=0.13,
        seg=24,
    ),
    "wing": dict(                 # 소형 생산자. 잠자리형, 양측에 큰 팬
        body_len=0.46, body_r=0.075,
        fan_r=0.20, fan_t=0.022, fan_out=0.17, blades=4,
        tail_len=0.20,
        seg=18,
    ),
    "fruitcrab": dict(            # 소형 생산자. 다리 4개, 강가 서식
        body_x=0.34, body_y=0.26, body_z=0.15,
        legs=4, leg_r=0.028, leg_len=0.26, leg_spread=0.20,
        eyes=2, eye_r=0.045, eye_stalk=0.10,
        seg=14,
    ),
}

# 불씨버섯 성장 단계. 설계 문서 4.1의 0→1→2 단계에 대응한다.
GROWTH = [("a", 0.45), ("b", 0.72), ("c", 1.0)]


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


def shade_flat(o):
    activate(o)
    bpy.ops.object.shade_flat()
    return o


def join(objs, name):
    """여러 조각을 하나의 메시로 합친다. 임시 모델이라 재질 분리는 하지 않는다."""
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
    bpy.ops.mesh.primitive_uv_sphere_add(radius=r, segments=seg, ring_count=max(6, seg // 2), location=c)
    return bpy.context.object


def cone(r1, r2, length, c=(0, 0, 0), seg=16, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cone_add(radius1=r1, radius2=r2, depth=length, vertices=seg, location=c)
    o = bpy.context.object
    o.rotation_euler = rot
    activate(o)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    return o


def sit_on_ground(obj):
    """오브젝트의 최저점을 원점 높이에 맞춘다. 배치할 때 지면에 파묻히지 않게."""
    activate(obj)
    lowest = min((obj.matrix_world @ v.co).z for v in obj.data.vertices)
    obj.location.z -= lowest
    bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)
    return obj


# ---------------------------------------------------------------- 식물군

def build_emberfungus(p, scale=1.0):
    """불씨버섯 — 갓이 아래로 처진 소형 발광 버섯."""
    parts = []
    s = scale

    stalk = rod(p["stalk_r"] * s, p["stalk_h"] * s,
                c=(0, 0, p["stalk_h"] * s * 0.5), seg=p["seg"])
    parts.append(stalk)

    # 갓: 위가 좁고 아래가 넓은 원뿔에 처짐을 준다
    cap_z = p["stalk_h"] * s
    cap = cone(p["cap_r"] * s, p["cap_r"] * s * 0.18, p["cap_h"] * s,
               c=(0, 0, cap_z + p["cap_h"] * s * 0.5), seg=p["seg"])
    # 아래쪽 링을 바깥+아래로 밀어 처진 갓을 만든다
    me = cap.data
    zs = [v.co.z for v in me.vertices]
    zmin = min(zs)
    for v in me.vertices:
        if abs(v.co.z - zmin) < 1e-5:
            v.co.x *= 1.0 + p["cap_droop"] * 0.30
            v.co.y *= 1.0 + p["cap_droop"] * 0.30
            v.co.z -= p["cap_h"] * s * p["cap_droop"]
    parts.append(cap)

    # 갓 아래 주름. 빛이 새어 나오는 곳이라 실루엣에 필요하다
    gill_r = p["cap_r"] * s * 0.62
    for i in range(p["gills"]):
        a = TAU * i / p["gills"]
        g = box(p["cap_r"] * s * 0.42, 0.006 * s, p["cap_h"] * s * 0.22,
                c=(math.cos(a) * gill_r * 0.55, math.sin(a) * gill_r * 0.55,
                   cap_z - p["cap_h"] * s * 0.02),
                rot=(0, 0, a))
        parts.append(g)

    obj = join(parts, "emberfungus")
    shade_flat(obj)
    return sit_on_ground(obj)


def build_mossnode(p):
    """매크로늄 이끼 — 바위에 깔린 얇은 덩어리. 매크로늄 맥의 지표종."""
    parts = []
    for i in range(p["lumps"]):
        a = TAU * i / p["lumps"] + 0.4
        d = p["radius"] * (0.30 + 0.55 * ((i * 7) % 5) / 5.0)
        r = p["radius"] * (0.26 + 0.16 * ((i * 3) % 4) / 4.0)
        h = p["height"] * (0.7 + 0.5 * ((i * 5) % 3) / 3.0)
        b = ball(r, c=(math.cos(a) * d, math.sin(a) * d, 0.0), seg=p["seg"])
        b.scale = (1.0, 1.0, h / r)
        activate(b)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        parts.append(b)

    obj = join(parts, "mossnode")
    shade_flat(obj)
    return sit_on_ground(obj)


def build_ashfern(p):
    """재양치 — 잔디 크기 양치류. 잎이 활처럼 휜다."""
    parts = []
    n = p["fronds"]
    for i in range(n):
        yaw = TAU * i / n
        # 잎대
        seg_n = p["leaflets"]
        for j in range(seg_n):
            t = (j + 0.5) / seg_n
            # 활 모양: 위로 올라갔다 바깥으로 처진다
            reach = p["frond_len"] * t
            height = p["frond_len"] * math.sin(t * math.pi * 0.62) * (1.0 - p["arch"] * t * 0.55)
            w = p["frond_w"] * (1.0 - 0.65 * t)
            leaf = box(w, 0.010, w * 0.30,
                       c=(math.cos(yaw) * reach, math.sin(yaw) * reach, height),
                       rot=(0, -0.55 * t, yaw))
            parts.append(leaf)

    # 밑동
    parts.append(rod(0.035, 0.07, c=(0, 0, 0.035), seg=10))

    obj = join(parts, "ashfern")
    shade_flat(obj)
    return sit_on_ground(obj)


# ---------------------------------------------------------------- 동물군

def build_eye(p):
    """눈 — 부양 분해자. 전방의 큰 눈이 주 탐지기관, 추진기 3개."""
    parts = []
    body = ball(p["body_r"], seg=p["seg"])
    body.scale = (1.0, 0.82, 0.88)
    activate(body)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    parts.append(body)

    # 눈은 +Y(블렌더 전방)로 튀어나온다
    eye = ball(p["eye_r"], c=(0, p["eye_out"], 0), seg=p["seg"])
    eye.scale = (1.0, 0.62, 1.0)
    activate(eye)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    parts.append(eye)
    # 홍채 링
    parts.append(rod(p["eye_r"] * 0.55, 0.02,
                     c=(0, p["eye_out"] + p["eye_r"] * 0.5, 0),
                     seg=p["seg"], rot=(math.pi / 2, 0, 0)))

    # 추진기
    for i in range(p["thrusters"]):
        a = TAU * i / p["thrusters"] - math.pi / 2
        cx = math.cos(a) * p["thruster_out"]
        cy = math.sin(a) * p["thruster_out"] * 0.6
        parts.append(rod(p["thruster_r"], p["thruster_len"],
                         c=(cx, cy, -p["body_r"] * 0.30), seg=12,
                         rot=(0, 0, 0)))

    obj = join(parts, "eye")
    shade_flat(obj)
    return obj


def build_ball(p):
    """공 — 구르는 분해자. 하부 구멍으로 잔해를 회수한다."""
    parts = []
    core = ball(p["r"], seg=p["seg"])
    parts.append(core)

    # 굴러가는 방향을 읽을 수 있게 띠를 두른다
    for i in range(p["bands"]):
        t = (i + 1) / (p["bands"] + 1)
        z = -p["r"] + 2 * p["r"] * t
        rr = math.sqrt(max(0.0, p["r"] ** 2 - z ** 2)) * 1.03
        band = rod(rr, p["band_t"], c=(0, 0, z), seg=p["seg"])
        parts.append(band)

    # 하부 흡입구
    intake = rod(p["intake_r"], p["r"] * 0.5, c=(0, 0, -p["r"] * 0.82), seg=16)
    parts.append(intake)

    obj = join(parts, "ball")
    shade_flat(obj)
    return obj


def build_wing(p):
    """날개 — 잠자리형 소형 생산자. 양측 팬으로 난다."""
    parts = []
    body = rod(p["body_r"], p["body_len"], c=(0, 0, 0), seg=p["seg"],
               rot=(math.pi / 2, 0, 0))
    parts.append(body)

    # 머리
    parts.append(ball(p["body_r"] * 1.25, c=(0, p["body_len"] * 0.5, 0), seg=p["seg"]))

    # 하부 스캔 장치 (도감: "몸체 하부에 장치된 스캔 장치가 탐지기관")
    parts.append(box(p["body_r"] * 1.1, p["body_r"] * 2.2, p["body_r"] * 0.5,
                     c=(0, p["body_len"] * 0.22, -p["body_r"] * 1.0)))

    # 양측 팬
    for side in (-1, 1):
        hub = rod(p["fan_r"] * 0.18, p["fan_t"] * 2.2,
                  c=(side * p["fan_out"], p["body_len"] * 0.1, 0), seg=12,
                  rot=(0, math.pi / 2, 0))
        parts.append(hub)
        for b in range(p["blades"]):
            a = TAU * b / p["blades"]
            parts.append(box(p["fan_t"], p["fan_r"], p["fan_r"] * 0.10,
                             c=(side * p["fan_out"],
                                p["body_len"] * 0.1 + math.cos(a) * p["fan_r"] * 0.5,
                                math.sin(a) * p["fan_r"] * 0.5),
                             rot=(a, 0, 0)))

    # 꼬리
    parts.append(cone(p["body_r"] * 0.8, p["body_r"] * 0.15, p["tail_len"],
                      c=(0, -p["body_len"] * 0.5 - p["tail_len"] * 0.5, 0),
                      seg=12, rot=(math.pi / 2, 0, 0)))

    obj = join(parts, "wing")
    shade_flat(obj)
    return obj


def build_fruitcrab(p):
    """열매게 — 다리 4개 소형 생산자. 눈 두 개가 앞으로 튀어나온다."""
    parts = []
    body = box(p["body_x"], p["body_y"], p["body_z"], c=(0, 0, p["leg_len"] * 0.55))
    parts.append(body)

    # 몸통 아래 입 (도감: "몸통 아래의 입으로 먹이를 담아")
    parts.append(rod(p["body_x"] * 0.30, p["body_z"] * 0.6,
                     c=(0, 0, p["leg_len"] * 0.55 - p["body_z"] * 0.55), seg=12))

    # 다리 4개
    for i in range(p["legs"]):
        sx = 1 if i % 2 == 0 else -1
        sy = 1 if i < 2 else -1
        hip = (sx * p["body_x"] * 0.45, sy * p["body_y"] * 0.45, p["leg_len"] * 0.55)
        # 허벅지: 바깥으로 벌어진다
        thigh_c = (hip[0] + sx * p["leg_spread"] * 0.5,
                   hip[1] + sy * p["leg_spread"] * 0.3,
                   p["leg_len"] * 0.72)
        parts.append(rod(p["leg_r"], p["leg_len"] * 0.55, c=thigh_c, seg=p["seg"],
                         rot=(sy * 0.6, -sx * 0.9, 0)))
        # 정강이: 아래로
        shin_c = (hip[0] + sx * p["leg_spread"],
                  hip[1] + sy * p["leg_spread"] * 0.6,
                  p["leg_len"] * 0.28)
        parts.append(rod(p["leg_r"] * 0.8, p["leg_len"] * 0.62, c=shin_c, seg=p["seg"]))

    # 눈자루 2개, 앞쪽(+Y)
    for i in range(p["eyes"]):
        sx = 1 if i == 0 else -1
        ex = sx * p["body_x"] * 0.25
        ey = p["body_y"] * 0.55
        parts.append(rod(p["eye_r"] * 0.35, p["eye_stalk"],
                         c=(ex, ey, p["leg_len"] * 0.55 + p["body_z"] * 0.5 + p["eye_stalk"] * 0.4),
                         seg=10, rot=(-0.5, 0, 0)))
        parts.append(ball(p["eye_r"],
                          c=(ex, ey + p["eye_stalk"] * 0.35,
                             p["leg_len"] * 0.55 + p["body_z"] * 0.5 + p["eye_stalk"] * 0.8),
                          seg=12))

    obj = join(parts, "fruitcrab")
    shade_flat(obj)
    return sit_on_ground(obj)


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
        bake_space_transform=True,   # 유니티에서 Transform 재적용이 필요 없게
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
    return path, len(obj.data.vertices), tri


# ---------------------------------------------------------------- 진입점

def main():
    rows = []

    # 불씨버섯 3단계 (성장 0/1/2)
    for suffix, scale in GROWTH:
        wipe()
        o = build_emberfungus(PARAMS["emberfungus"], scale)
        rows.append(("emberfungus_" + suffix,) + export(o, "emberfungus_" + suffix)[1:])

    builders = [
        ("mossnode", lambda: build_mossnode(PARAMS["mossnode"])),
        ("ashfern", lambda: build_ashfern(PARAMS["ashfern"])),
        ("eye", lambda: build_eye(PARAMS["eye"])),
        ("ball", lambda: build_ball(PARAMS["ball"])),
        ("wing", lambda: build_wing(PARAMS["wing"])),
        ("fruitcrab", lambda: build_fruitcrab(PARAMS["fruitcrab"])),
    ]
    for name, fn in builders:
        wipe()
        o = fn()
        rows.append((name,) + export(o, name)[1:])

    print("")
    print("=" * 58)
    print("  생성 결과")
    print("=" * 58)
    print("  %-22s %8s %8s" % ("이름", "정점", "삼각형"))
    print("  " + "-" * 40)
    for name, v, t in rows:
        print("  %-22s %8d %8d" % (name, v, t))
    print("=" * 58)
    print("  출력: %s" % os.path.normpath(OUT))


if __name__ == "__main__":
    main()
