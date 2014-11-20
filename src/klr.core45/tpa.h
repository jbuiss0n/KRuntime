#pragma once

BOOL CreateTpaBase(LPWSTR** ppNames, size_t* pcNames);
BOOL FreeTpaBase(const LPWSTR* values, const size_t count);