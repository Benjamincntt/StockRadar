#!/usr/bin/env bash
# Spec 016 đợt 4 (T039) — xuất 26 bảng V1 ra /tmp TRƯỚC khi migration RemoveV1Tables xoá chúng.
# DO-NOT-CHANGE: script chỉ đọc (SELECT + bcp out); không thêm DROP/DELETE/TRUNCATE — dữ liệu V1 mất là không khôi phục được.
set -euo pipefail

APP_PRODUCTION_CONFIG="/var/www/publish/stockradar-api/appsettings.Production.json"
SQLCMD="/opt/mssql-tools18/bin/sqlcmd"
BCP="/opt/mssql-tools18/bin/bcp"

NGAY="$(date +%Y%m%d)"
THU_MUC="/tmp/v1-export-${NGAY}"
TEP_NEN="/tmp/v1-export-${NGAY}.tar.gz"

# 26 bảng V1 — đúng Phụ lục A spec 016 / danh sách chốt 12/11/2026. Thiếu một bảng là mất dữ liệu vĩnh viễn.
CAC_BANG=(
    Alerts
    CriterionGroupDailyAccuracies
    CriterionGroupWeeklyReviews
    CriterionWeights
    DailyAnalysisRuns
    DailyCriterionAccuracies
    DailyOpportunities
    EarlyRecoveryRadar
    EntryTimingStates
    FalsePositiveMiningStates
    HitCalibrationBuckets
    HitCalibrationStates
    MasterAlertPositions
    PersonalCalibrationStates
    PositionSellLegs
    SectorWaveRegimes
    SessionRadarHits
    SetupTracks
    ShadowWeightPicks
    ShadowWeightSummaries
    StockCriterionDetails
    StockCriterionScores
    TradeJournalEntries
    VipAlertFires
    WeeklyCriterionReviews
    WeeklyOpportunityReviews
)

# Lấy một khoá trong chuỗi kết nối: lay_gia_tri 'Server' -> giá trị sau "Server=".
lay_gia_tri() {
    printf '%s' "${CHUOI_KET_NOI}" | grep -oPi "(?:^|;)\s*$1\s*=\s*\K[^;]+" | head -n1 | tr -d '\r'
}

chay_sqlcmd() {
    "${SQLCMD}" -S "${MAY_CHU}" -d "${CO_SO_DU_LIEU}" -U "${NGUOI_DUNG}" -C -b -l 60 -h -1 -W -Q "$1"
}

so_chu_so() {
    tr -d '[:space:]'
}

if [[ ! -r "${APP_PRODUCTION_CONFIG}" ]]; then
    printf 'KHÔNG đọc được %s — dừng, chưa xuất gì.\n' "${APP_PRODUCTION_CONFIG}" >&2
    exit 1
fi
if [[ ! -x "${SQLCMD}" || ! -x "${BCP}" ]]; then
    printf 'Thiếu %s hoặc %s — dừng.\n' "${SQLCMD}" "${BCP}" >&2
    exit 1
fi

CHUOI_KET_NOI="$(grep -oP '"DefaultConnection"\s*:\s*"\K[^"]+' "${APP_PRODUCTION_CONFIG}" | head -n1)"
MAY_CHU="$(lay_gia_tri 'Server')"
CO_SO_DU_LIEU="$(lay_gia_tri 'Database')"
NGUOI_DUNG="$(lay_gia_tri 'User Id')"

# SECURITY: mật khẩu chỉ nằm trong SQLCMDPASSWORD, không in ra màn hình và không ghi xuống file nào.
# bcp không đọc biến này nên phải truyền -P trực tiếp; vì vậy script không bật set -x và không echo lệnh bcp.
export SQLCMDPASSWORD="$(lay_gia_tri 'Password')"

if [[ -z "${MAY_CHU}" || -z "${CO_SO_DU_LIEU}" || -z "${NGUOI_DUNG}" || -z "${SQLCMDPASSWORD}" ]]; then
    printf 'Chuỗi kết nối thiếu Server/Database/User Id/Password — dừng.\n' >&2
    exit 1
fi

mkdir -p "${THU_MUC}"
: > "${THU_MUC}/counts.txt"
: > "${THU_MUC}/so-sanh.txt"

SO_BANG_KHOP=0
SO_BANG_LECH=0

printf 'Xuất 26 bảng V1 vào %s (%s / %s)\n\n' "${THU_MUC}" "${MAY_CHU}" "${CO_SO_DU_LIEU}"

for bang in "${CAC_BANG[@]}"; do
    hien_co="$(chay_sqlcmd "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.tables WHERE name = '${bang}';" | so_chu_so)"
    if [[ "${hien_co}" != "1" ]]; then
        printf '%s\tKHÔNG CÓ TRONG DB\n' "${bang}" | tee -a "${THU_MUC}/so-sanh.txt"
        SO_BANG_LECH=$((SO_BANG_LECH + 1))
        continue
    fi

    # (a) COUNT(*) — số đối chiếu duy nhất đáng tin (sys.partitions bị nhân theo đơn vị lưu trữ).
    dem="$(chay_sqlcmd "SET NOCOUNT ON; SELECT COUNT_BIG(*) FROM dbo.[${bang}];" | so_chu_so)"
    printf '%s\t%s\n' "${bang}" "${dem}" >> "${THU_MUC}/counts.txt"

    # (b) Tên cột theo đúng thứ tự để đọc lại file .dat sau này.
    chay_sqlcmd "SET NOCOUNT ON; SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = '${bang}' ORDER BY ORDINAL_POSITION;" \
        > "${THU_MUC}/${bang}.columns.txt"

    # (c) Dữ liệu thô: tách trường bằng US (0x1F), tách dòng bằng RS (0x1E) nên dấu phẩy/xuống dòng/JSON không làm vỡ file.
    # DO-NOT-CHANGE: bcp trên Linux tin chứng chỉ tự ký bằng -u (không phải -C như sqlcmd); thiếu -u là lỗi SSL, không xuất được bảng nào.
    if ! ket_qua_bcp="$("${BCP}" "dbo.${bang}" out "${THU_MUC}/${bang}.dat" \
        -S "${MAY_CHU}" -d "${CO_SO_DU_LIEU}" -U "${NGUOI_DUNG}" -P "${SQLCMDPASSWORD}" \
        -u -C 65001 -c -t 0x1F -r 0x1E -l 60 2>&1)"; then
        printf '%s: bcp lỗi — dừng, chưa nén gì.\n%s\n' "${bang}" "${ket_qua_bcp}" >&2
        exit 1
    fi

    # (d) Đếm số dòng thật trong file bằng số ký tự RS rồi so với COUNT(*).
    dong_tep="$(tr -cd '\036' < "${THU_MUC}/${bang}.dat" | wc -c | so_chu_so)"
    if [[ "${dong_tep}" == "${dem}" ]]; then
        ket_qua="KHỚP"
        SO_BANG_KHOP=$((SO_BANG_KHOP + 1))
    else
        ket_qua="LỆCH"
        SO_BANG_LECH=$((SO_BANG_LECH + 1))
    fi

    printf '%-32s COUNT(*)=%-8s dòng trong file=%-8s %s\n' "${bang}" "${dem}" "${dong_tep}" "${ket_qua}" \
        | tee -a "${THU_MUC}/so-sanh.txt"
done

tar -czf "${TEP_NEN}" -C /tmp "$(basename "${THU_MUC}")"

KICH_THUOC="$(du -h "${TEP_NEN}" | cut -f1)"
SHA256="$(sha256sum "${TEP_NEN}" | cut -d' ' -f1)"

printf '\n==> File nén: %s (%s)\n' "${TEP_NEN}" "${KICH_THUOC}"
printf '==> sha256: %s\n' "${SHA256}"
printf '==> Tổng %d bảng: KHỚP %d, LỆCH %d\n' "${#CAC_BANG[@]}" "${SO_BANG_KHOP}" "${SO_BANG_LECH}"

if (( SO_BANG_LECH > 0 )); then
    printf 'CÓ BẢNG LỆCH — KHÔNG ship migration cho tới khi làm rõ.\n' >&2
    exit 2
fi

printf 'Xong. Tải %s về máy, kiểm sha256, rồi xoá %s và %s trên server.\n' "${TEP_NEN}" "${THU_MUC}" "${TEP_NEN}"
