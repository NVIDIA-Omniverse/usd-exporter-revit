// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
#pragma once

#include "ExportApi.h"

#include <string>
#include <vector>

namespace StringUtil
{
void ToUpper(std::string& s);
void ToLower(std::string& s);
bool StartWith(const std::string& s, const std::string& start);
bool EndWith(const std::string& s, const std::string& tail);

void Split(const std::string& source, const char tok, std::vector<std::string>& output);
void SplitAndTrim(const std::string& source, const char tok, std::vector<std::string>& output);
int Exist(const std::vector<std::string>& source, const std::string& str);

void ReplaceChar(std::string& in, char from, char to);
std::string& Trim(std::string& s);
void ReplaceStr(std::string& in, const std::string& from, const std::string& to);

/**
 * In Windows environment, when returning a UTF-8 string from C#, the characters are garbled.
 * Function to avoid this.
 */
void GetRawData(const char* ptr, const void** data, int* size);
} // namespace StringUtil

extern "C"
{
    /**
     * Converts string on the C# side to UTF-8 string.
     * @param[in]  ptr    String passed from C#
     * @param[out] data   Returns a non-conversion string buffer.
     * @param[out] size   Returns a non-conversion string buffer size.
     */
    USD_EXPORTER_REVIT_API void stringutil_getRawData(const char* ptr, const void** data, int* size);
}
