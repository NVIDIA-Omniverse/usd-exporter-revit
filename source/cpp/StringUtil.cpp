// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
#include "StringUtil.h"

#include <algorithm>
#ifdef WIN32
#include <io.h>
#else
#include <sys/types.h>
#endif // WIN32

namespace StringUtil
{
void ToUpper(std::string& s)
{
    std::transform(s.begin(), s.end(), s.begin(), ::toupper);
}

void ToLower(std::string& s)
{
    std::transform(s.begin(), s.end(), s.begin(), ::tolower);
}

bool StartWith(const std::string& s, const std::string& start)
{
    if (s.size() < start.size())
    {
        return false;
    }
    return (s.compare(0, start.size(), start) == 0);
}

bool EndWith(const std::string& s, const std::string& tail)
{
    if (s.size() < tail.size())
    {
        return false;
    }
    return (s.compare(s.size() - tail.size(), tail.size(), tail) == 0);
}

void Split(const std::string& source, const char tok, std::vector<std::string>& output)
{
    output.clear();
    std::string::size_type pos2 = source.find(tok);
    std::string::size_type pos1 = 0;
    while (std::string::npos != pos2)
    {
        if (pos2 > pos1)
        {
            output.push_back(source.substr(pos1, pos2 - pos1));
        }
        pos1 = pos2 + 1;
        pos2 = source.find(tok, pos1);
    }
    std::string tmp = source.substr(pos1);
    if (tmp.size() > 0)
    {
        output.push_back(tmp);
    }
}

void SplitAndTrim(const std::string& source, const char tok, std::vector<std::string>& output)
{
    StringUtil::Split(source, tok, output);
    for (size_t i = 0; i < output.size(); ++i)
    {
        StringUtil::Trim(output[i]);
    }
}

int Exist(const std::vector<std::string>& source, const std::string& str)
{
    int i = 0;
    for (auto& s : source)
    {
        if (s == str)
        {
            return i;
        }
        ++i;
    }
    return -1;
}

void ReplaceChar(std::string& in, char from, char to)
{
    for (auto& c : in)
    {
        if (c == from)
        {
            c = to;
        }
    }
}

std::string& Trim(std::string& s)
{
    if (s.empty())
    {
        return s;
    }

    s.erase(0, s.find_first_not_of(" \t\r\n\b"));
    s.erase(s.find_last_not_of(" \t\r\n\b") + 1);
    return s;
}

void ReplaceStr(std::string& in, const std::string& from, const std::string& to)
{
    std::string::size_type pos = 0;
    std::string::size_type srclen = from.size();
    std::string::size_type dstlen = to.size();
    while ((pos = in.find(from, pos)) != std::string::npos)
    {
        in.replace(pos, srclen, to);
        pos += dstlen;
    }
}

void GetRawData(const char* ptr, const void** data, int* size)
{
    *data = nullptr;
    *size = 0;
    if (ptr == nullptr)
    {
        return;
    }

    *data = ptr;
    *size = std::string(ptr).size();
}
} // namespace StringUtil

// ------------------------------------------------------------.
extern "C"
{
    REVIT_USD_EXPORT_API void stringutil_getRawData(const char* ptr, const void** data, int* size)
    {
        StringUtil::GetRawData(ptr, data, size);
    }
}
