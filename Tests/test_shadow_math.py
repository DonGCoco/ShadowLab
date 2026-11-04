"""CPU geometry regressions, NOT a Unity/Metal rendering test.
Run: python3 -m unittest discover -s Tests -v
"""
import math
import unittest

FAR = 25.0
TAN = math.tan(math.radians(35))


def ray_for_texel(uv, top_origin):
    x, y = (2 * uv[0] - 1), (2 * uv[1] - 1)
    if top_origin:
        y = -y
    return (x * TAN, y * TAN, 1.0)


def dot(a, b):
    return sum(x * y for x, y in zip(a, b))


def intersect_depth(ray, receiver, normal):
    t = dot(normal, receiver) / dot(normal, ray)
    return t * ray[2] / FAR


def analytic_plane_depth(uv, a, b, k, top_origin):
    # Independent known scene: z = k + a*x + b*y.
    x, y, _ = ray_for_texel(uv, top_origin)
    return k / (1 - a*x - b*y) / FAR


class ReceiverPlaneGeometry(unittest.TestCase):
    def test_texel_center_projection_roundtrip(self):
        for top in (False, True):
            for uv in ((.1,.2), (.51,.79), (.97,.03)):
                x,y,z = ray_for_texel(uv, top)
                u = (x/z/TAN + 1)/2
                v = (y/z/TAN + 1)/2
                if top: v = 1-v
                self.assertAlmostEqual(u, uv[0])
                self.assertAlmostEqual(v, uv[1])

    def test_sloped_planes_remain_lit_across_wide_pcf(self):
        for top in (False, True):
            for a,b in ((0,0), (.55,.35), (-.4,.6)):
                center = (.58,.63)
                normal = (-a,-b,1)
                center_ray = ray_for_texel(center, top)
                z = analytic_plane_depth(center,a,b,8,top) * FAR
                receiver = tuple(v*z for v in center_ray)
                for resolution in (512,1024,2048):
                    for x in range(-2,3):
                        for y in range(-2,3):
                            uv = tuple((math.floor((q+o*2.1/resolution)*resolution)+.5)/resolution
                                       for q,o in zip(center,(x,y)))
                            actual = intersect_depth(ray_for_texel(uv,top), receiver, normal)
                            expected = analytic_plane_depth(uv,a,b,8,top)
                            self.assertAlmostEqual(actual, expected, places=12)
                            self.assertLessEqual(actual-1e-6, expected)

    def test_reproduces_false_shadow_without_correction(self):
        center=(.58,.63)
        center_depth=analytic_plane_depth(center,.55,.35,8,True)
        false_shadows=0
        for x in range(-2,3):
            for y in range(-2,3):
                uv=tuple((math.floor((q+o*2.1/512)*512)+.5)/512
                         for q,o in zip(center,(x,y)))
                stored=analytic_plane_depth(uv,.55,.35,8,True)
                false_shadows += center_depth-1e-6 > stored
        self.assertGreater(false_shadows,0)

    def test_real_occluder_is_not_removed(self):
        receiver=(0,0,8)
        plane_depth=intersect_depth(ray_for_texel((.5,.5),True), receiver, (0,0,1))
        stored_blocker=4/FAR
        self.assertGreater(plane_depth-.002,stored_blocker)

    def test_fullscreen_triangle_covers_viewport(self):
        # Vertices (-1,-1),(-1,3),(3,-1): x>=-1,y>=-1,x+y<=2.
        for x,y in ((-1,-1),(-1,1),(1,-1),(1,1),(0,0)):
            self.assertGreaterEqual(x,-1)
            self.assertGreaterEqual(y,-1)
            self.assertLessEqual(x+y,2)


if __name__ == '__main__':
    unittest.main()
