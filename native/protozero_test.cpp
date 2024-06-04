#include "protozero/include/protozero/pbf_writer.hpp"
#include <iostream>
#include <limits>
#include <cstdint>

#ifdef _MSC_VER
#define EXPORT __declspec(dllexport)
#else
#define EXPORT
#endif

extern "C" {
    EXPORT int WriteProto(int messagesCount, unsigned char* output)
    {
        std::string data;
        protozero::pbf_writer pbf_root{data};
        for (int j = 0; j < messagesCount; ++j)
        {
            protozero::pbf_writer pbf_example{pbf_root, 1};
            pbf_example.add_uint64(1, std::numeric_limits<uint64_t>::max());
            pbf_example.add_int64(2, std::numeric_limits<int64_t>::min());
            pbf_example.add_string(4, "Hello, World!");
            pbf_example.add_string(5, "Msg 1");
            pbf_example.add_string(5, "Msg 2");
            pbf_example.add_string(5, "Msg 3");
            pbf_example.add_string(5, "Msg 4");

            for (int i = 0; i < 9; ++i)
            {
                protozero::pbf_writer pbf_sub{pbf_example, 99999};

                pbf_sub.add_uint64(1, i);
                pbf_sub.add_string(2, "Inner Message");
            }
        }
        std::copy(data.begin(), data.end(), output);
        return data.length();
    }
}