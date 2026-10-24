"""Blender 4.2: make_plinth.py -- <out.fbx>. Native Z-up, exported Y-up."""
import math
import os
import sys

import bmesh
import bpy


def main():
    output = os.path.abspath(sys.argv[sys.argv.index('--') + 1])
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, -0.3))
    obj = bpy.context.object
    obj.name = 'Plinth'
    obj.scale.z = 0.6
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    # Bake the translation into the mesh so the object origin is the top centre.
    for vertex in obj.data.vertices:
        vertex.co.z -= 0.3
    obj.location.z = 0
    mesh = obj.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    top_edges = [edge for edge in bm.edges
                 if all(abs(vertex.co.z) < 1e-6 for vertex in edge.verts)]
    bmesh.ops.bevel(bm, geom=top_edges, offset=0.04, segments=2,
                    affect='EDGES', profile=0.5)
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()
    for name in ('Plinth_Top', 'Plinth_Sides'):
        mesh.materials.append(bpy.data.materials.new(name))
    uv = mesh.uv_layers.new(name='UVMap')
    colors = mesh.color_attributes.new(name='Col', type='BYTE_COLOR', domain='CORNER')
    mesh.color_attributes.active_color = colors
    ink = (0x1E / 255, 0x1A / 255, 0x2E / 255, 1)
    for face in mesh.polygons:
        top = face.normal.z > 0.999
        bevel = 0.001 < face.normal.z < 0.999
        face.material_index = 0 if top else 1
        for loop_index in face.loop_indices:
            point = mesh.vertices[mesh.loops[loop_index].vertex_index].co
            if top:
                texcoord = (point.x + 0.5, point.y + 0.5)
                # The inset top still spans the complete texture.
                texcoord = ((texcoord[0] - 0.04) / 0.92,
                            (texcoord[1] - 0.04) / 0.92)
            else:
                # Four adjacent quarters, with the seam at the south-west corner.
                nx, ny = face.normal.x, face.normal.y
                if abs(ny) >= abs(nx):
                    perimeter = point.x + 0.5 if ny < 0 else 2.5 - point.x
                else:
                    perimeter = point.y + 1.5 if nx > 0 else 3.5 - point.y
                texcoord = (perimeter / 4, (point.z + 0.6) / 0.6)
            uv.data[loop_index].uv = texcoord
            colors.data[loop_index].color_srgb = ink if bevel else (1, 1, 1, 1)
    os.makedirs(os.path.dirname(output), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=output, use_selection=True, object_types={'MESH'},
                             axis_forward='-Z', axis_up='Y',
                             apply_scale_options='FBX_SCALE_UNITS',
                             bake_space_transform=True, use_mesh_modifiers=True,
                             mesh_smooth_type='FACE', colors_type='SRGB',
                             add_leaf_bones=False, bake_anim=False)
    print(f'Exported {len(mesh.vertices)} vertices to {output}')


if __name__ == '__main__':
    main()
