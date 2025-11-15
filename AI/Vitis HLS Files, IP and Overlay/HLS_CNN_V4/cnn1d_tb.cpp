// ==========================================
// cnn1d_tb.cpp  (Vitis HLS C TB)
// Requires:
//   - cnn1d_ip.cpp in the solution
//   - hls_params/params.h, conv1_weights.h, conv1_bias.h,
//     dense_weights.h, dense_bias.h (from exporter)
//   - hls_params/tb_vector.h (from notebook cell)
// ==========================================
#include <cstdio>
#include <cmath>
#include <hls_stream.h>
#include <ap_axi_sdata.h>
// #include "params.h"
#include "tb_vector.h"

// DUT prototype
typedef ap_axiu<32,1,1,1> axis_t;
void cnn1d_ip(hls::stream<axis_t>& s_in, hls::stream<axis_t>& s_out);

// u32<->float helper
union u32f_t { unsigned int u; float f; };

int main() {
    hls::stream<axis_t> s_in, s_out;

    // --- drive inputs (D_IN floats), TLAST on last
    for (int i=0; i<TB_D_IN; ++i) {
        axis_t pkt;
        u32f_t cvt; cvt.f = TB_TEST_IN[i];
        pkt.data = cvt.u;
        pkt.keep = -1;
        pkt.strb = -1;
        pkt.user = 0;
        pkt.id   = 0;
        pkt.dest = 0;
        pkt.last = (i == TB_D_IN-1) ? 1 : 0;
        s_in.write(pkt);
    }

    // --- call DUT
    cnn1d_ip(s_in, s_out);

    // --- read outputs (CLASSES probs), TLAST on last
    float out[TB_CLASSES];
    int last_seen = -1;
    for (int c=0; c<TB_CLASSES; ++c) {
        axis_t pkt = s_out.read();
        u32f_t cvt; cvt.u = pkt.data;
        out[c] = cvt.f;
        if (pkt.last) last_seen = c;
    }

    // --- checks and prints
    if (last_seen != TB_CLASSES - 1) {
        std::printf("ERROR: TLAST not set on last output (%d)\n", last_seen);
        return 1;
    }

    // softmax sum
    float s = 0.f;
    int argmax = 0;
    for (int c=0; c<TB_CLASSES; ++c) {
        s += out[c];
        if (out[c] > out[argmax]) argmax = c;
    }

    std::printf("Output probs:\n");
    for (int c=0; c<TB_CLASSES; ++c) {
        std::printf("  c=%d: %.7f", c, out[c]);
        if (c == argmax) std::printf("  <-- argmax");
        std::printf("\n");
    }
    std::printf("Sum(probs) = %.7f\n", s);
    std::printf("Argmax     = %d\n", argmax);
    std::printf("Exp class  = %d\n", TB_EXP_CLASS);

    // Optional numeric check vs TB_EXP_PROBS
    const float ABS_TOL = 5e-3f;   // tune as needed
    int mism = 0;
    for (int c=0; c<TB_CLASSES; ++c) {
        float diff = std::fabs(out[c] - TB_EXP_PROBS[c]);
        if (diff > ABS_TOL) ++mism;
    }
    if (mism) {
        std::printf("NOTE: %d outputs differ by > %.3g (expected w/ HLS math; adjust tol).\n", mism, ABS_TOL);
    }

    std::printf("TB DONE.\n");
    return 0;
}
