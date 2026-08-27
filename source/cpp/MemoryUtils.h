// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//

#include <algorithm> // for std::copy()
#include <cstddef> // for std::size_t
#include <iterator> // for std::begin()

template <class InputIt, class OutputIt>
bool copyMemoryBuffer(InputIt first, InputIt last, OutputIt d_first, std::size_t srcSize, std::size_t dstSize)
{
    if (srcSize == dstSize)
    {
        std::copy(first, last, d_first);
        return true;
    }
    else
    {
        return false;
    }
}
