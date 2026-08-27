// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//

#include "PrimvarData.h"

namespace revit::usd_export::core
{

// explicitly instantiate each of the types we defined in the public header.
template class PrimvarData<float>;
template class PrimvarData<int64_t>;
template class PrimvarData<int>;
template class PrimvarData<std::string>;
template class PrimvarData<pxr::TfToken>;
template class PrimvarData<pxr::GfVec2f>;
template class PrimvarData<pxr::GfVec3f>;

} // namespace revit::usd_export::core
