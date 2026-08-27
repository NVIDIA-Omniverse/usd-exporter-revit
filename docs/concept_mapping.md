# NVIDIA OpenUSD Exporter Plugin for Revit to OpenUSD Data Mapping

## Introduction

### Overview

This document describes how the NVIDIA OpenUSD Exporter Plugin for Revit maps
Autodesk Revit model data to OpenUSD. It focuses on current plugin and SDK
behavior.

The plugin runs inside Autodesk Revit on Windows and exports one or more
OpenUSD layers from a chosen 3D view. It reads view-visible elements,
transforms, materials, tessellated geometry, lights, and linked models, then
authors OpenUSD output for visualization, review, analysis, and downstream
Omniverse and OpenUSD scene workflows.

The primary audience is:

- Users who need to understand what Revit data appears in exported USD.
- Developers integrating the SDK into custom Revit-to-OpenUSD pipelines.
- Downstream tools that need reliable guidance about export options and output
  structure.
- AI agents that need reliable source material for choosing export options and
  explaining output USD structure.

### Reference Versions

Plugin behavior depends on the Autodesk Revit version, OpenUSD runtime, and
plugin/SDK versions included with a release. Supported host versions are Revit
2024, 2025, and 2026. Check package metadata and release notes for exact
dependency versions.

This document separates implemented output from future or proposed mappings.
Behavioral claims describe the plugin release documented by the corresponding
package metadata and release notes.

### General Assumptions and Constraints

This mapping describes one-way export from a Revit document into OpenUSD. The
exporter does not preserve enough information for general USD-to-Revit round
trip.

The output layer must remain internally consistent: stage metrics, transforms,
mesh points, material bindings, BIM attributes, visibility, and composition
arcs must describe the same scene scale and hierarchy. Unit handling is
described in [Units and Stage Metrics](#units-and-stage-metrics).

The exporter can serialize only data available from the chosen 3D view, loaded
elements, appearance assets, parameters, spatial volumes, link instances, and
related Revit data that the exporter reads. Source concepts that Revit does
not expose, or that are filtered out by the export view, cannot be
reconstructed reliably in this exporter.

Export is view-driven. Only elements visible in the selected 3D view are
exported. Revit view visibility, section box, phase filter, detail level, and
temporary view settings therefore define which source concepts can appear in
USD.

Some Revit concepts are useful conceptual matches for OpenUSD schemas but are
not emitted by the current exporter. Those concepts are listed in
[What Is Not Preserved](#what-is-not-preserved), not mixed into current
behavior.

### What Is Not Preserved

The current NVIDIA OpenUSD Exporter Plugin for Revit does not preserve the
following regardless of export options. Option-gated concepts (rooms, spaces,
links, lights, cameras, drawings, BIM attributes) are covered in the concept
mapping table and later sections.

- Enough source data for general Revit round trip.
- Native Revit solid or B-rep topology as an OpenUSD B-rep schema. Geometry is
  emitted as tessellated `UsdGeomMesh` or, for eligible pipes, conduits, and
  round ducts, as `UsdGeomCylinder`.
- Levels, grids, reference planes, dimensions, or annotations as dedicated
  hierarchy or annotation prims. Levels are used only when placing 2D drawing
  quads.
- Revit schedules, constraints, design options, or full parametric
  relationships.
- Physics joints or collision APIs derived from Revit constraints.
- Typed BIM parameters with native units. Exported BIM parameters are string
  attributes using Revit display values.
- USD relationships for Revit joins, hosts, room boundaries, or IFC-style
  spatial containment reconstructed from Revit.
- Exact Autodesk material networks for every appearance asset schema. Unhandled
  schemas fall back to color and transparency heuristics or user MDL mappings.
- Multi-UV sets beyond a single `primvars:st` channel.
- Sky, environment, or date/time/location stage content.
- Arc curve element geometry. Those elements are skipped with a warning.

### Definitions, Acronyms, and Abbreviations

| Term | Description |
| --- | --- |
| NVIDIA OpenUSD Exporter Plugin for Revit | NVIDIA Revit plugin that exports Autodesk Revit models to OpenUSD. |
| Revit | Autodesk Revit host application. |
| AEC | Architecture, engineering, and construction data. |
| BIM | Building Information Modeling data such as categories, families, types, parameters, rooms, spaces, and links. |
| Element | Unique placed object in a Revit document (`Autodesk.Revit.DB.Element`). Floors, doors, walls, beams, and furniture are typical elements. |
| Category | Revit category grouping such as Walls, Doors, Floors, or Rooms (`Autodesk.Revit.DB.Category`). |
| Family | External family definition (`.rfa`) loaded into the model that groups related types and geometry (`Autodesk.Revit.DB.Family`). |
| Family type | Named type variant within a family (`Autodesk.Revit.DB.FamilySymbol`). |
| Family instance | Placed element that references a family type (`Autodesk.Revit.DB.FamilyInstance`). |
| Cylindrical element | Straight pipe, conduit, or round duct whose `LocationCurve` is a line and whose diameter parameters allow export as `UsdGeomCylinder` instead of a dense mesh. |
| Spatial element | Non-physical volume (Room or Space) bounded by physical elements such as walls and ceilings (`Autodesk.Revit.DB.SpatialElement`; rooms as `Architecture.Room`, spaces as `Mechanical.Space`). |
| Light source | Light-emitting object on a Lighting Fixture family (`Autodesk.Revit.DB.Lighting.LightType`). Fixture geometry stays under `Instances/`; the optional `UsdLux` prim is authored under `Lights/`. |
| Link | Revit linked model (`.rvt`) composed into the host document (`Autodesk.Revit.DB.LinkInstance`). |
| Export view | The 3D view selected for export (`Autodesk.Revit.DB.View3D`). It filters visible elements and drives camera and section-box behavior. |
| Appearance asset | Revit material rendering asset used to derive OmniPBR or OmniGlass materials (`Autodesk.Revit.DB.Visual.Asset`). |
| MPU | USD `metersPerUnit` stage metric. |
| MDL | Material Definition Language shaders such as OmniPBR and OmniGlass. |

## Concept Mapping Summary

This table gives the high-level Revit to OpenUSD mapping performed by the
plugin. Later sections describe behavior, option gates, and important limits.

| Revit Concept | Current OpenUSD Result | Controls | Consumer Impact |
| --- | --- | --- | --- |
| Revit document / project | Root `SdfLayer` and `UsdStage` with default prim | Output folder, file name, extension | One export becomes one root USD stage, with optional material, family, and link sub-stages. |
| Export 3D view | Visibility filter and optional active camera | Chosen view, `View.DetailLevel`, `View.PhaseFilter`, `View.ViewTemplate` | Only view-visible content is exported. |
| Default prim / file name | Root `UsdGeomXform`, `kind=assembly` | `File.FileName` | Root assembly name and default prim path. |
| Coordinate system | Optional child `UsdGeomXform` under default prim | `CoordinateSystem` | Geometry root can be Internal Origin, Project Base Point, Survey Point, or Shared Coordinates. |
| Category | `UsdGeomScope` under `Instances/{Category}` | Source category name | Category becomes addressable hierarchy. |
| Family | `UsdGeomScope` `{FamilyName}` | Source family name | Family grouping is preserved in prim path. |
| Family type | `UsdGeomScope` `{TypeName}` | Source type name; omitted in external family instancing | Type grouping is preserved unless external family assets flatten under the family. |
| Element instance | `UsdGeomXform` `{elementId}`, `kind=component` | View visibility, include options | Instance placement, display name, and optional BIM attributes are addressable. |
| Element geometry | `UsdGeomMesh` `Mesh_{materialId}` | Detail level, view section box | Consumers receive triangulated meshes with normals and UVs. |
| Pipe / conduit / round duct | `UsdGeomCylinder` `Cylinder` | Straight `LocationCurve` line and diameter parameters | Straight pipes, conduits, and round ducts can be compact cylinders; curved runs remain meshes. |
| Revit material | `UsdShadeMaterial` under Looks library | `MaterialStyle`, `MaterialFolderName`, user MDL mappings | Visual appearance remains queryable and bindable. |
| Material binding | `UsdShadeMaterialBindingAPI` on mesh or cylinder | Material processing | Geometry binds to Looks materials. |
| Family instancing | Internal class reference, external family asset reference, or payload | `InstanceFamilies`, `FamilyInstanceStyle` | Repeated family geometry can be shared. |
| User family-type mapping | Payload to external USD asset | `Mappings.FamilyTypes.UserMapped` | Custom assets can replace Revit family geometry. User mappings are always payloads. |
| Revit link | Payload under `RVT Links/` to a link stage | `IncludeLinks` | Linked models remain separate composed USD files. |
| Room / Space | Xform + mesh under `Instances/Rooms` or `Instances/Spaces` | `IncludeRooms`, `IncludeSpaces`, color scheme names | Spatial volumes can be exported, then hidden after write. |
| Lighting fixture | Fixture Xform and meshes under `Instances/`; additional `UsdLux` light under `Lights/` | `IncludeLights` controls the light, not fixture geometry | Sphere, cylinder, rect, or disk lights; optional IES files. |
| 3D view camera | `UsdGeomCamera` under `Cameras/` | Active view always; `IncludeCameras` for all non-template 3D views | Camera placement is available for review workflows. With `IncludeCameras=true`, the active view is exported a second time with a collision-safe prim-name suffix. |
| 2D drawing / sheet view | Textured quad under `Drawings/{PublishSet}/{ViewType}/` | `IncludeDrawings`, `DrawingPublishSet` | Sheet/view images can be placed as textured meshes, then hidden after write. |
| BIM parameters | `BIM:Instance:*` and `BIM:Type:*` string attributes | `IncludeBimData` | Parameter-name suffixes are encoded as valid USD identifiers; original names become attribute display names when encoding changes them. |
| Workset | `BIM:Instance:Workset` string | `IncludeBimData`, workshared document | Workset membership remains queryable. |

## Current Converter Behavior

### Stage, Document, and Root Prim

The exporter creates a main USD stage for the output layer and configures a
default prim. The default prim is an `Xform` and is assigned `kind=assembly`.
The default prim name is derived from `File.FileName`.

Stage creation starts in Revit internal units:

- Up axis: `Z`
- Initial `metersPerUnit`: `0.3048` (feet)

After geometry and materials are authored, the exporter converts the stage to
the selected `UnitType`. See [Units and Stage Metrics](#units-and-stage-metrics).

There is no dedicated Document or Project metadata prim. Document title is used
to organize link stage paths and related output layout.

### Export View and Visibility Filter

Export begins from a chosen 3D view. Before export, the exporter can
temporarily apply:

- `View.DetailLevel`
- `View.PhaseFilter`
- `View.ViewTemplate`

Only elements visible in that view are exported. Section boxes, category
visibility, and phase state in the export view therefore act as the primary
source filters. After write, Rooms, Spaces, Drawings, and Cameras are set
invisible on the stage. Lights remain visible.

### Coordinate Systems

`CoordinateSystem` controls whether geometry is parented under an extra Xform
below the default prim:

| Value | Name | Behavior |
| --- | --- | --- |
| `0` | Internal Origin | No coordinate child. `Instances`, `Prototypes`, and `Cameras` live directly under the default prim. |
| `1` | Project Base Point | Child Xform `project_base_point` becomes the geometry root. |
| `2` | Survey Point | Child Xform `survey_point` becomes the geometry root. |
| `3` | Shared Coordinates | Child Xform `shared_coordinates` becomes the geometry root. |

When a non-internal coordinate system is selected, Cameras, Instances, and
Prototypes are parented under that coordinate Xform. Lights and Drawings attach
under the default prim, not under the coordinate child. Linked-model payload
prims also live under the default prim in `RVT Links/`.

### Element Hierarchy

Visible Revit elements become a Category → Family → Type → ElementId tree under
`Instances/`:

```text
/{FileName}                            # default prim, kind=assembly
├── {coordinate_system}?               # optional geometry root
│   ├── Instances/
│   │   ├── {Category}/
│   │   │   ├── {FamilyName}/
│   │   │   │   ├── {FamilyTypeName}/  # omitted for external family instancing
│   │   │   │   │   └── {elementId}/   # Xform, kind=component
│   │   │   │   │       ├── Mesh_{materialId}
│   │   │   │   │       └── Cylinder
│   │   ├── Rooms/
│   │   └── Spaces/
│   ├── Prototypes/
│   └── Cameras/
├── Looks/                             # or external Looks stage
├── RVT Links/
├── Drawings/
│   └── {PublishSet}/{ViewType}/{View}/
└── Lights/
```

Hierarchy naming:

- Category scope name = Revit category name.
- Family scope name = Revit family name.
- Type scope name = Revit type name, except when external family instancing
  flattens instance Xforms directly under the family.
- Leaf Xform name = element id string.
- Elements without a type, such as some toposolids, create an Xform directly
  under the category.

Authored prim names are encoded to valid USD identifiers. When the encoded
identifier differs from the original Revit name, the exporter preserves the
original name as display name.

Leaf element Xforms are assigned `kind=component`. The default prim and
coordinate-system Xform use assembly kind where authored.

### Geometry and Meshes

Revit element geometry becomes `UsdGeomMesh` children named `Mesh_{materialId}`.
Current mesh authoring includes:

- Triangulated `faceVertexCounts` and `faceVertexIndices`
- `points`
- Vertex normals
- `primvars:st` UV primvars; missing or mismatched UVs are zero-filled
- Material binding to the corresponding Looks material
- `doNotCastShadows` for glass materials

For non-instanced elements, instance transforms can be baked into mesh points.
For instanced families, prototype geometry stays at the origin and the instance
Xform carries placement. Mirrored family instances apply basis correction before
transform authoring.

Eligible pipes, conduits, and round ducts author a `UsdGeomCylinder` named
`Cylinder` only when their `LocationCurve` is a straight `Line`. Curved runs
continue through tessellated mesh export. Cylinder radius comes from `Diameter`
for pipes and ducts, and from `Outside Diameter` for conduits.

### Family Instancing and Prototypes

`InstanceFamilies` and `FamilyInstanceStyle` control reusable family geometry:

| Style | Current Handling |
| --- | --- |
| `None` | Unique meshes per element; no family prototype sharing. |
| `InternalClasses` | Prototype class `{TypeName}_{n}` under `Prototypes/{Category}/{FamilyName}/`; instances use internal references and can be marked instanceable. |
| `ExternalAssetAsReference` | Family sidecar stage under `Families/` with a `FamilyType` variant set; instances reference the family asset. |
| `ExternalAssetAsPayload` | Same external family layout, composed by payload. |

External family stages are named from the family name and family id, with
suffixes added when needed to avoid output-path collisions. Each family stage
defines a `FamilyType` variant set and may include sibling geometry and Looks
files. Internal class materials are copied below
`Instance/{MaterialFolderName}/`.

Instance eligibility requires a single family-type part with meshes and no
root-level meshes. Curtain Wall Mullions, Curtain Panels, and Parking are never
instanced.

User family-type mappings in `Mappings.FamilyTypes.UserMapped` replace Revit
family geometry with an external USD asset payload and skip geometry export for
mapped types. `FamilyInstanceStyle` does not change this payload behavior.

### Linked Models

When `IncludeLinks=true`, each linked model becomes an Xform under
`RVT Links/` with a payload to a separate link stage:

```text
{OutputFolder}/Links/{docTitle}/{linkName}_{index}.usdc
```

Links are always payloads. Nested links create additional `RVT Links` scopes
inside parent link stages. Link transforms are applied on the link Xform. Link
materials are processed into the link Looks stage. Rooms and Spaces can also be
authored inside link stages when those options are enabled.

Link payload arcs contain the asset path only and therefore target the link
stage default prim.

When `IncludeLinks=false`, link traversal is skipped.

### Rooms and Spaces

Rooms and Spaces are optional spatial volumes:

- `IncludeRooms` with `RoomColorScheme`
- `IncludeSpaces` with `SpaceColorScheme`

Export requires a resolved Color Fill Scheme name. Name resolution is
case-insensitive and tries equality, prefix, suffix, then substring matching.
Only Rooms and Spaces with `Area > 0` are exported. Their solids are
triangulated into meshes under `Instances/Rooms` or `Instances/Spaces`. Scheme
entry colors become synthetic materials. Active section boxes can filter and
crop spatial solids.

Authored Rooms and Spaces are hidden after stage write and do not cast shadows.
They are visualization aids, not editable Revit spatial objects.

### Cameras, Lights, and Drawings

| Source | USD Result | Gate |
| --- | --- | --- |
| Active export 3D view | `UsdGeomCamera` under `Cameras/` | Always attempted for the export view |
| All non-template 3D views, including the active export view | Additional `UsdGeomCamera` prims | `IncludeCameras` |
| Lighting fixtures | Existing fixture geometry under `Instances/` plus a `UsdLux` sphere, cylinder, rect, or disk light under `Lights/` | `IncludeLights` adds the `UsdLux` light |
| IES profiles | Files under `./IES/` when output is local | `IncludeLights` and local output path |
| Views in a publish set | Textured quad meshes under `Drawings/{PublishSet}/{ViewType}/` with JPEG images under `./Drawings/` | `IncludeDrawings`, `DrawingPublishSet` |

Drawings without an active crop box or non-printable views are skipped. Drawing
and camera prims are hidden after write. Each drawing Xform and its mesh are
named after the source view. Drawing meshes contain one four-vertex face rather
than triangulated element geometry. Lights remain visible.

### Materials, Looks Library, and Textures

Revit materials become `UsdShadeMaterial` prims under a Looks library.

| Source | USD Output |
| --- | --- |
| Appearance asset such as Generic, Metal, Glass, Concrete | OmniPBR or OmniGlass MDL materials with Preview Surface support |
| No appearance asset | Fallback from material color / transparency |
| User MDL mapping | Custom MDL asset path and module |
| Room/space scheme colors | Synthetic OmniGlass-like materials |
| Drawing images | OmniPBR with albedo texture |

For fallback materials, glass detection uses material name heuristics
(`glas` / `glaz`) or `Material.Transparency > 10`. Supported appearance schemas
apply their schema-specific conversion. Glass meshes receive
`doNotCastShadows=true`.

`MaterialStyle` controls Looks composition:

| Style | Behavior |
| --- | --- |
| `InternalLibrary` | Looks authored on the main stage |
| `ExternalLibraryAsReference` | Separate `{MaterialFolderName}.usdc` referenced by the default prim |
| `ExternalLibraryAsPayload` | Separate Looks stage composed by payload |

Textures are resolved from appearance asset paths or Autodesk shared material
texture folders, then copied beside the Looks library when the output path is
local.

### BIM Metadata Attributes

When `IncludeBimData=true`, element Xforms receive custom string attributes:

| Attribute prefix | Source |
| --- | --- |
| `BIM:Instance:Workset` | Workset name when the document is workshared |
| `BIM:Instance:ElementId` | Element id |
| `BIM:Instance:Category` | Category name |
| `BIM:Instance:{parameter}` | Instance parameter display values |
| `BIM:Type:Name` | Type element name |
| `BIM:Type:{parameter}` | Type parameter display values |

Element-id parameters resolve to referenced element names when possible.

BIM data is authored as flat string attributes, not typed USD schemas, typed
quantity attributes, or USD relationships for Revit joins/hosts.

### Units and Stage Metrics

Export authors geometry first in Revit feet, then converts stage metrics:

| Stage property | Value |
| --- | --- |
| Up axis | `Z` |
| Initial linear unit | Feet (`metersPerUnit = 0.3048`) |
| Final linear unit | Selected `UnitType` |

Supported `UnitType` values:

| Enum | Final interpretation |
| --- | --- |
| `Feet` | Keep feet |
| `Inches` | Convert to inches |
| `Meters` | Convert to meters |
| `Centimeters` | Convert to centimeters |
| `Millimeters` | Convert to millimeters |
| `Micrometers` | Convert to micrometers |
| `Nanometers` | Convert to nanometers |

Conversion scales xform translations and pivots, mesh points, extents, and
cylinder radius/height. Instanceable prims also scale local scale. External
mapped assets are scaled by asset MPU relative to feet before final conversion.

## Export Options That Affect Mapping

Names in this table match plugin export settings fields.

| Option | Values and Defaults | Mapping Impact |
| --- | --- | --- |
| `File.OutputFolder` | Documents/Omniverse/Revit by default | Chooses local output directory. |
| `File.FileName` | `"Default"` | Names the default prim and root file stem. |
| `File.Extension` | `.usdc` default; `.usd` / `.usda` / `.usdc` supported | Chooses USD file format. |
| `View.DetailLevel` | Empty default | Temporarily sets Fine/Medium/Coarse on the export view. |
| `View.PhaseFilter` | Empty default | Temporarily applies a phase filter. |
| `View.ViewTemplate` | Empty default | Temporarily applies a view template. |
| `IncludeCameras` | `false` default | Exports all non-template 3D view cameras in addition to the always-exported active view; the active view can therefore appear twice. |
| `IncludeLights` | `false` default | Adds `UsdLux` prims for lighting fixtures; fixture Xforms and meshes remain under `Instances/`. |
| `IncludeLinks` | `false` default | Exports linked models as payload stages under `RVT Links/`. |
| `IncludeBimData` | `false` default | Authors `BIM:Instance:*` and `BIM:Type:*` string attributes. |
| `IncludeRooms` / `RoomColorScheme` | `false` / empty | Exports room volumes colored by matching Color Fill Scheme. |
| `IncludeSpaces` / `SpaceColorScheme` | `false` / empty | Exports MEP space volumes colored by matching Color Fill Scheme. |
| `IncludeDrawings` / `DrawingPublishSet` | `false` / empty | Exports publish-set views as textured drawing quads. |
| `InstanceFamilies` | `false` default | Enables family instancing. |
| `FamilyInstanceStyle` | `None` (0), `InternalClasses` (1), `ExternalAssetAsReference` (2), `ExternalAssetAsPayload` (3) | Chooses internal class prototypes or external family assets. |
| `CoordinateSystem` | `0` Internal Origin, `1` Project Base Point, `2` Survey Point, `3` Shared Coordinates | Chooses geometry-root coordinate child. |
| `UnitType` | `Feet` default | Chooses final `metersPerUnit` conversion. |
| `MaterialStyle` | `ExternalLibraryAsReference` default | Chooses internal Looks, external reference, or external payload. |
| `MaterialFolderName` | `"Looks"` | Names the materials scope or sidecar stage. |
| `Mappings.Materials.UserMapped` | Empty list | Overrides Revit materials with user MDL asset/module pairs. |
| `Mappings.FamilyTypes.UserMapped` | Empty list | Replaces family types with external USD assets. |

## Example USD Shapes

Exact prim names depend on Revit names, default prim names, coordinate mode,
instancing mode, and USD identifier encoding. These examples show structural
shape only.

### Category / Family / Type / Element Mesh

```usda
def Xform "Project" (
    kind = "assembly"
)
{
    def Scope "Instances"
    {
        def Scope "Walls"
        {
            def Scope "Basic_Wall"
            {
                def Scope "Generic_200mm"
                {
                    def Xform "123456" (
                        kind = "component"
                    )
                    {
                        def Mesh "Mesh_789"
                        {
                            int[] faceVertexCounts = [3, 3, ...]
                            int[] faceVertexIndices = [...]
                            point3f[] points = [...]
                            normal3f[] normals = [...]
                            texCoord2f[] primvars:st = [...]
                        }
                    }
                }
            }
        }
    }
}
```

### Internal Family Prototype Reference

```usda
def Xform "Project" (
    kind = "assembly"
)
{
    def Scope "Prototypes"
    {
        def Scope "Doors"
        {
            def Scope "Single_Flush"
            {
                def Class "0915_x_2134mm_0"
                {
                    def Xform "Instance"
                    {
                        def Scope "Looks" { ... }
                        def Mesh "Mesh_12" { ... }
                    }
                }
            }
        }
    }

    def Scope "Instances"
    {
        def Scope "Doors"
        {
            def Scope "Single_Flush"
            {
                def Scope "0915_x_2134mm"
                {
                    over "234567" (
                        instanceable = true
                        prepend references = </Project/Prototypes/Doors/Single_Flush/0915_x_2134mm_0/Instance>
                    )
                    {
                        matrix4d xformOp:transform = ...
                    }
                }
            }
        }
    }
}
```

### Linked Model Payload

```usda
def Xform "Project"
{
    def Scope "RVT Links"
    {
        def Xform "Architecture_0" (
            prepend payload = @Links/HostTitle/Architecture_0.usdc@
        )
        {
            matrix4d xformOp:transform = ...
        }
    }
}
```

### BIM Attributes

```usda
def Xform "123456" (
    kind = "component"
)
{
    custom string "BIM:Instance:ElementId" = "123456"
    custom string "BIM:Instance:Category" = "Walls"
    custom string "BIM:Instance:Mark" = "A-101"
    custom string "BIM:Type:Name" = "Generic - 200mm"
    custom string "BIM:Type:Width" = "200"
}
```

### Room Volume

```usda
def Scope "Instances"
{
    def Scope "Rooms"
    {
        def Xform "345678" (
            kind = "component"
        )
        {
            def Mesh "Mesh_-1"
            {
                # triangulated room solid
            }
        }
    }
}
```

## Format-Specific Concepts

### View-Driven Export

Unlike file converters that load a whole CAD document, this plugin exports from
a Revit 3D view. Hidden categories, temporary hide/isolate, phase filters,
detail level, and section boxes in that view define the source set. A full-model
export requires a view that shows the intended content.

### Rooms, Spaces, and Color Fill Schemes

Room and Space export depends on Color Fill Scheme name matching, not on IFC
spatial structure relationships. The exporter does not author USD relationships
for room boundaries, room separation lines, or occupancy relationships.

## Appendices

### Appendix A: Tessellated Geometry to `UsdGeomMesh`

Revit element geometry is exported as triangulated meshes. Current mesh output
maps that tessellation to `UsdGeomMesh` and material binding concepts. Drawing
image meshes are an exception: each drawing uses one four-vertex quad face.

| Revit Tessellation Data | Current OpenUSD Mapping | Notes |
| --- | --- | --- |
| Triangle vertices | `UsdGeomMesh` topology and points | Source triangles become `faceVertexCounts=3`, `faceVertexIndices`, and `points`. |
| Vertex normals | Vertex normal data | Authored when present. |
| UV coordinates | `primvars:st` | Zero-filled when missing or mismatched. |
| Material assignment | Mesh name `Mesh_{materialId}` and material binding | Multiple materials on one element become multiple mesh children. |
| Glass material | Shadow casting disabled | Glass meshes do not cast shadows. |

Eligible pipes, conduits, and round ducts can author `UsdGeomCylinder` instead
of dense triangle meshes.

### Appendix B: Mesh and Instance Granularity

A single Revit element can produce:

- One or more `Mesh_{materialId}` children
- One `Cylinder` child for eligible MEP runs
- An internal class reference or external family asset composition arc
- An external user-mapped asset payload

The exporter prefers one Xform per element id. Material differences split mesh
children rather than splitting the element Xform. Family prototypes share
geometry across instances when instancing is enabled and eligibility checks
pass.

### Appendix C: Attribute Mapping

Revit parameters become custom USD string attributes when `IncludeBimData=true`.

| Revit Source | Current USD Attribute |
| --- | --- |
| Workset name | `BIM:Instance:Workset` |
| Element id | `BIM:Instance:ElementId` |
| Category name | `BIM:Instance:Category` |
| Instance parameter display value | `BIM:Instance:{parameter name}` |
| Type name | `BIM:Type:Name` |
| Type parameter display value | `BIM:Type:{parameter name}` |

Parameter names are encoded with the USD identifier encoder before becoming
attribute-name suffixes. If encoding changes a name, the original Revit
parameter name is preserved as the attribute display name.

The exporter authors USD attributes, not custom USD metadata dictionaries. This
keeps BIM values inspectable in USD editors. Values are display strings, not
typed doubles with unit metadata.
