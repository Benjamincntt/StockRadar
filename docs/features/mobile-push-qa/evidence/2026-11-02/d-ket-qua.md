KICH BAN D — tin he-thong, KHONG co symbol, app chay nen (HOME, pid 22150 con song)
================================================================================
May vivo V2359A 10AE4G1ZE2000K8, app 3.0.1 (build 30/10 14:43), production a7af905.
Trang thai truoc khi gui: focus=com.bbk.launcher2 (HOME), pid=22150, stopped=false, so_tin_com_juice=0.

Payload (xem d-payload.json, da che token): android.notification{channel_id=he-thong,
title="HPO - xong (TEST)", body="TEST KICH BAN D - khong phai tin that, khong co symbol."},
android.priority=normal, android.ttl=43200s, data={nguon=he_thong}.
Khuon dung nhu KenhDayTinFcm.TaoTinNhan nhanh heThong (title/body nam trong android.notification,
KHONG co notification goc).

FCM chap nhan: ten=projects/juice-stockradar/messages/0:1793584414111768%3c4e8fcb3c4e8fcb
  => epoch FCM = 1793584414111

May (dumpsys notification --noredact):
  NotificationRecord pkg=com.juice.stockradar id=0 tag=FCM-Notification:124538706
  channel=he-thong importance=3 flags=AUTO_CANCEL
  when=1793584414840
  => DO SDK FIREBASE TU HIEN THI (chuong dang "FCM-Notification:" la dang tri tuyet doi;
     tin do app tu show thi tag=null va id=hash cua messageId).
  Do tre FCM chap nhan -> may hien notify: 1793584414840 - 1793584414111 = 729 ms.

Logcat cung thoi diem (08:53:34 may):
  W/JobInfo: Job 'com.juice.stockradar/io.flutter.plugins.firebase.messaging.FlutterFirebaseMessagingBackgroundService#2020' has a deadline...
  I/FLTFireBGExecutor: Creating background FlutterEngine instance.
  I/FLTFireMsgService: FlutterFirebaseMessagingBackgroundService started!
  => app o nen nen plugin chay isolate nen (goi _tinDayNen top-level, no-op), SDK tu hien notify.

Cham vao notify (uiautomator bounds title = [266,599][716,665] -> tap 491,632):
  mCurrentFocus = com.juice.stockradar/com.juice.stockradar.MainActivity
  pid van la 22150 (KHOI DONG NONG, khong phai lanh)
  so_tin_com_juice = 0 sau khi cham (AUTO_CANCEL)
  Man hien: Trang chu (d-02-cham-xong-mo-app-trang-chu.png), khong crash, khong dieu huong.

KET LUAN D: DAT — dung ky vong "chi mo app, khong vang loi" (khong co symbol => khong dieu huong,
push_service.dart:171-175 duongDanTuTin tra ve null).
