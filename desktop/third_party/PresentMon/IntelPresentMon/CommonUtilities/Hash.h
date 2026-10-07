// Copyright (C) 2022 Intel Corporation
// SPDX-License-Identifier: MIT
#pragma once
#include <utility>

struct _GUID;

namespace pmon::util::hash
{
	size_t HashCombine(size_t lhs, size_t rhs) noexcept;

	template <typename T, typename S>
	size_t DualHash(const T& t, const S& s) noexcept
	{
		return HashCombine(std::hash<T>{}(t), std::hash<S>{}(s));
	}

	size_t HashGuid(const _GUID&) noexcept;
}

namespace std
{
	template <class T, class S>
	struct hash<std::pair<T, S>>
	{
		size_t operator()(const std::pair<T, S>& p) const noexcept
		{
			return pmon::util::hash::DualHash(p.first, p.second);
		}
	};
}