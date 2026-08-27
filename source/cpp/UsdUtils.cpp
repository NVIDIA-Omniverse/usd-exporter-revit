// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//

#include "UsdUtils.h"

#include "Log.h"
#include "SdfUtils.h"

#include <pxr/base/tf/stringUtils.h>
#include <pxr/base/tf/token.h>
#include <pxr/base/vt/value.h>
#include <pxr/usd/sdf/schema.h>


using namespace pxr;

bool revit::usd_export::core::detail::isEditablePrimLocation(const UsdStagePtr stage, const SdfPath& path, std::string* reason)
{
    // The stage must be valid
    if (!stage)
    {
        if (reason != nullptr)
        {
            *reason = "Invalid UsdStage.";
        }
        return false;
    }

    // The path must be a valid absolute prim path
    if (!path.IsAbsolutePath() || !path.IsPrimPath())
    {
        if (reason != nullptr)
        {
            *reason = TfStringPrintf("\"%s\" is not a valid absolute prim path.", revit::usd_export::core::detail::getPathAsString(path).c_str());
        }
        return false;
    }

    // Any existing prim must not be an instance proxy
    const UsdPrim prim = stage->GetPrimAtPath(path);
    if (prim && prim.IsInstanceProxy())
    {
        if (reason != nullptr)
        {
            *reason = TfStringPrintf("\"%s\" is an instance proxy, authoring is not allowed.", revit::usd_export::core::detail::getPathAsString(path).c_str());
        }
        return false;
    }

    // Check if the path is the descendant of an instance
    // Walk up the path hierarchy until we reach "/"
    SdfPath currentPath = path.GetParentPath();
    while (currentPath != SdfPath::AbsoluteRootPath())
    {
        const UsdPrim currentPrim = stage->GetPrimAtPath(currentPath);
        if (currentPrim)
        {
            if (currentPrim.IsInstance())
            {
                if (reason != nullptr)
                {
                    *reason = TfStringPrintf(
                        "\"%s\" is a descendant of instance \"%s\", authoring is not allowed.",
                        revit::usd_export::core::detail::getPathAsString(path).c_str(),
                        revit::usd_export::core::detail::getPathAsString(currentPath).c_str()
                    );
                }
                return false;
            }
            else if (currentPrim.IsInstanceProxy())
            {
                if (reason != nullptr)
                {
                    *reason = TfStringPrintf(
                        "\"%s\" is a descendant of instance proxy \"%s\", authoring is not allowed.",
                        revit::usd_export::core::detail::getPathAsString(path).c_str(),
                        revit::usd_export::core::detail::getPathAsString(currentPath).c_str()
                    );
                }
                return false;
            }
            else
            {
                // If we found a prim that is neither an instance nor an instance proxy,
                // then the hierarchy above it is safe and we can return true
                return true;
            }
        }
        currentPath = currentPath.GetParentPath();
    }

    return true;
}

bool revit::usd_export::core::detail::isEditablePrimLocation(const UsdPrim& prim, const std::string& name, std::string* reason)
{
    // The parent prim must be valid
    // We don't need to check that the UsdStage is valid as it must be if the UsdPrim is valid.
    if (!prim)
    {
        if (reason != nullptr)
        {
            *reason = "Invalid UsdPrim";
        }
        return false;
    }

    // The parent prim must not be an instance
    if (prim.IsInstance())
    {
        if (reason != nullptr)
        {
            *reason = TfStringPrintf("\"%s\" is an instance, authoring is not allowed.", revit::usd_export::core::detail::getPathAsString(prim.GetPath()).c_str());
        }
        return false;
    }

    // The parent prim must not be an instance proxy
    if (prim.IsInstanceProxy())
    {
        if (reason != nullptr)
        {
            *reason = TfStringPrintf("\"%s\" is an instance proxy, authoring is not allowed.", revit::usd_export::core::detail::getPathAsString(prim.GetPath()).c_str());
        }
        return false;
    }

    // The name must be a valid identifier
    if (!SdfPath::IsValidIdentifier(name))
    {
        if (reason != nullptr)
        {
            *reason = TfStringPrintf("\"%s\" is not a valid prim name", name.c_str());
        }
        return false;
    }

    // Any existing prim must not be an instance proxy
    const UsdPrim child = prim.GetChild(TfToken(name));
    if (child && child.IsInstanceProxy())
    {
        if (reason != nullptr)
        {
            *reason = TfStringPrintf("\"%s\" is an instance proxy, authoring is not allowed.", revit::usd_export::core::detail::getPathAsString(child.GetPath()).c_str());
        }
        return false;
    }

    return true;
}
