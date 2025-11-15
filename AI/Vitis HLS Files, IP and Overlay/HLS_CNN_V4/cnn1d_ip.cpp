// ==========================================
// cnn1d_ip.cpp  (Ultra96V2 / PYNQ, low-resource)
// Conv1D(ReLU) → Flatten → Dense  (same math as before)
// AXI-Stream FP32 in/out + AXI-Lite; TLAST handled
// Outputs LOGITS (softmax on PS)
// ==========================================
#include <hls_stream.h>
#include <ap_int.h>
#include <ap_axi_sdata.h>
#include <math.h>

#include "params.h"          // D_IN, CONV1_OUT, KERNEL (=3), CLASSES
#include "conv1_weights.h"   // CONV1_W[CONV1_OUT][KERNEL]
#include "conv1_bias.h"      // CONV1_B[CONV1_OUT]
#include "dense_weights.h"   // DENSE_W[D_IN*CONV1_OUT][CLASSES]
#include "dense_bias.h"      // DENSE_B[CLASSES]

typedef ap_axiu<32,1,1,1> axis_t;
union u32f_t { unsigned int u; float f; };

void cnn1d_ip(hls::stream<axis_t>& s_in,
              hls::stream<axis_t>& s_out) {
    // ---------------- Interfaces ----------------
    #pragma HLS INTERFACE axis      port=s_in
    #pragma HLS INTERFACE axis      port=s_out
    #pragma HLS INTERFACE s_axilite port=return bundle=CTRL

    // ---- Keep resources small ----
    // Put big const tables in BRAM as ROMs (avoid LUT/SRL explosion)
    #pragma HLS RESOURCE variable=CONV1_W core=ROM_1P
    #pragma HLS RESOURCE variable=CONV1_B core=ROM_1P
    #pragma HLS RESOURCE variable=DENSE_W core=ROM_1P
    #pragma HLS RESOURCE variable=DENSE_B core=ROM_1P

    // Hard limit DSP usage (time-multiplex multipliers)
    #pragma HLS ALLOCATION instances=mul limit=180 operation

    // ---------------- Locals ----------------
    float x[D_IN];
    #pragma HLS BIND_STORAGE variable=x type=ram_1p impl=bram

    float logits[CLASSES];
    #pragma HLS ARRAY_PARTITION variable=logits complete dim=1  // small vector

    // ---------------- Read input ----------------
    read_in:
    for (int i=0; i<D_IN; ++i) {
        #pragma HLS PIPELINE II=1
        axis_t pkt = s_in.read();
        u32f_t cvt; cvt.u = pkt.data;
        x[i] = cvt.f;
    }

    // ---------------- Init logits ----------------
    init_logits:
    for (int c=0; c<CLASSES; ++c) {
        #pragma HLS PIPELINE II=1
        logits[c] = DENSE_B[c];
    }

    // ---------------- Fused compute ----------------
    // Use a 3-tap sliding window so we read x[] only once per iter.
    float xm1 = 0.f;
    float x0  = (D_IN > 0) ? x[0] : 0.f;
    float x1  = (D_IN > 1) ? x[1] : 0.f;

    main_fused:
    for (int t=0; t<D_IN; ++t) {
        #pragma HLS PIPELINE II=2  // relaxed II to cut resource pressure

        const float v_m1 = xm1;
        const float v_0  = x0;
        const float v_p1 = x1;

        // advance window (only ONE new read per iter)
        xm1 = x0;
        x0  = x1;
        x1  = (t+2 < D_IN) ? x[t+2] : 0.f;

        // For each filter, compute conv act and immediately fold into dense
        const int base = t * CONV1_OUT;

        filters_loop:
        for (int f=0; f<CONV1_OUT; ++f) {
            // No UNROLL (single MAC reused) -> tiny DSP usage
            float acc = CONV1_B[f];
            acc += v_m1 * CONV1_W[f][0];
            acc += v_0  * CONV1_W[f][1];
            acc += v_p1 * CONV1_W[f][2];
            if (acc < 0.f) acc = 0.f;

            // Dense accumulation across classes (serialized)
            classes_loop:
            for (int c=0; c<CLASSES; ++c) {
                logits[c] += acc * DENSE_W[base + f][c];
            }
        }
    }

    // ---------------- Write logits ----------------
    write_out:
    for (int c=0; c<CLASSES; ++c) {
        #pragma HLS PIPELINE II=1
        axis_t pkt;
        u32f_t cvt; cvt.f = logits[c];
        pkt.data = cvt.u;
        pkt.keep = -1;
        pkt.strb = -1;
        pkt.user = 0;
        pkt.id   = 0;
        pkt.dest = 0;
        pkt.last = (c == (CLASSES - 1)) ? 1 : 0;
        s_out.write(pkt);
    }
}
