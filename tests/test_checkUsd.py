# SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
# SPDX-License-Identifier: Apache-2.0

import os

from pxr import Gf, Usd, UsdGeom


class CheckUsd:
    _stage = None
    _testC = None

    def __init__(self, usd_file_path: str, _test):
        self._stage = Usd.Stage.Open(usd_file_path)
        self._testC = _test

    def __del__(self):
        self._stage = None

    def get_metersPerUnit(self):
        metersPerUnit = UsdGeom.GetStageMetersPerUnit(self._stage)
        return metersPerUnit

    def get_displayName(self, primPath: str):
        prim = self._stage.GetPrimAtPath(primPath)
        if not prim.IsValid():
            return ""

        for primSpec in prim.GetPrimStack():
            displayName = primSpec.GetInfo("displayName")
            if displayName is not None and displayName != "":
                if isinstance(displayName, str):
                    return displayName
                return ""
        return ""

    def get_defaultPrim(self):
        defaultPrim = self._stage.GetDefaultPrim()
        return defaultPrim.GetPath()

    def get_primTypeName(self, primPath):
        prim = self._stage.GetPrimAtPath(primPath)
        if not prim.IsValid():
            return ""

        return prim.GetTypeName()

    def get_childPrimNames(self, primPath):
        prim = self._stage.GetPrimAtPath(primPath)
        if not prim.IsValid():
            return None

        names = []
        pChildren = prim.GetChildren()
        for cPrim in pChildren:
            names.append(cPrim.GetName())
        return names

    def get_boundingBoxSize(self, primPath):
        prim = self._stage.GetPrimAtPath(primPath)
        if not prim.IsValid():
            return None

        # Calc world boundingBox.
        bboxCache = UsdGeom.BBoxCache(Usd.TimeCode.Default(), ["default"])
        bboxD = bboxCache.ComputeWorldBound(prim).ComputeAlignedRange()
        bb_min = Gf.Vec3f(bboxD.GetMin())
        bb_max = Gf.Vec3f(bboxD.GetMax())
        return bb_max - bb_min

    def get_localTranslation(self, primPath):
        prim = self._stage.GetPrimAtPath(primPath)
        if not prim.IsValid():
            return None

        returned = self._get_local_transform_components(prim)
        if returned == None:
            return None
        return returned[0]

    def get_localPivot(self, primPath):
        prim = self._stage.GetPrimAtPath(primPath)
        if not prim.IsValid():
            return None

        returned = self._get_local_transform_components(prim)
        if returned == None:
            return None
        return returned[1]

    def _get_local_transform_components(self, prim, time=None):
        if time is None:
            time = Usd.TimeCode.Default()

        # Initialize as identity
        translation = Gf.Vec3d(0.0, 0.0, 0.0)
        pivot = Gf.Vec3d(0.0, 0.0, 0.0)
        rotation = Gf.Vec3f(0.0, 0.0, 0.0)
        rotation_order = "xyz"  # Default rotation order
        scale = Gf.Vec3f(1.0, 1.0, 1.0)

        # Early out if the prim is not xformable
        xformable = UsdGeom.Xformable(prim)
        if not xformable:
            return (translation, pivot, rotation, rotation_order, scale)

        # Attempt to extract existing xformOp values
        xform_common_api = UsdGeom.XformCommonAPI(prim)
        if xform_common_api:
            # Extract transform components
            return self._get_xform_vectors_by_accumulation(xform_common_api, time)

        # Compute the local transform matrix and populate the result from that
        matrix = xformable.GetLocalTransformation(time)
        if matrix is not None:
            return self._compute_components_from_matrix(matrix)

        return (translation, pivot, rotation, rotation_order, scale)

    def _get_xform_vectors_by_accumulation(self, xform_common_api, time):
        pivot_float = Gf.Vec3f()
        rot_order = UsdGeom.XformCommonAPI.RotationOrderXYZ
        translation, rotation, scale, pivot_float, rot_order = xform_common_api.GetXformVectors(time)
        pivot = Gf.Vec3d(pivot_float[0], pivot_float[1], pivot_float[2])
        rotation_order = self._convert_rotation_order(rot_order)
        return (translation, pivot, rotation, rotation_order, scale)

    def _compute_components_from_matrix(self, matrix):
        transform = Gf.Transform(matrix)
        rotation_double = self._compute_xyz_rotations_from_rotation(transform.GetRotation())
        rotation = Gf.Vec3f(rotation_double[0], rotation_double[1], rotation_double[2])
        rotation_order = "xyz"
        scale_double = transform.GetScale()
        scale = Gf.Vec3f(scale_double[0], scale_double[1], scale_double[2])

        return (transform.GetTranslation(), transform.GetPivotPosition(), rotation, rotation_order, scale)

    def _convert_rotation_order(self, rot_order):
        if rot_order == UsdGeom.XformCommonAPI.RotationOrderXYZ:
            return "xyz"
        elif rot_order == UsdGeom.XformCommonAPI.RotationOrderXZY:
            return "xzy"
        elif rot_order == UsdGeom.XformCommonAPI.RotationOrderYXZ:
            return "yxz"
        elif rot_order == UsdGeom.XformCommonAPI.RotationOrderYZX:
            return "yzx"
        elif rot_order == UsdGeom.XformCommonAPI.RotationOrderZXY:
            return "zxy"
        elif rot_order == UsdGeom.XformCommonAPI.RotationOrder.ZYX:
            return "zyx"
        else:
            return "xyz"  # Default fallback

    def _compute_xyz_rotations_from_rotation(self, rotation):
        angles = rotation.Decompose(Gf.Vec3d.ZAxis(), Gf.Vec3d.YAxis(), Gf.Vec3d.XAxis())
        return Gf.Vec3d(angles[2], angles[1], angles[0])
