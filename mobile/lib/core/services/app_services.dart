import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../api/api_client.dart';
import '../models/models.dart';

class AuthService extends ChangeNotifier {
  AuthService(this._api);

  static const _tokenKey = 'stockradar_token';
  static const _userKey = 'stockradar_user';

  final ApiClient _api;
  AuthUser? _user;

  AuthUser? get user => _user;
  bool get isLoggedIn => _user != null && _user!.token.isNotEmpty;

  Future<void> load() async {
    final prefs = await SharedPreferences.getInstance();
    final token = prefs.getString(_tokenKey);
    final email = prefs.getString('${_userKey}_email');
    final displayName = prefs.getString('${_userKey}_displayName');
    final userId = prefs.getString('${_userKey}_userId');
    if (token != null && email != null) {
      _user = AuthUser(
        userId: userId ?? '',
        email: email,
        displayName: displayName ?? email,
        token: token,
      );
      _api.setToken(token);
      notifyListeners();
    }
  }

  Future<void> login(String email, String password) async {
    final result = await _api.login(email, password);
    await _persist(result);
  }

  Future<void> register(String email, String password, String displayName) async {
    final result = await _api.register(email, password, displayName);
    await _persist(result);
  }

  Future<void> _persist(AuthUser user) async {
    _user = user;
    _api.setToken(user.token);
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_tokenKey, user.token);
    await prefs.setString('${_userKey}_email', user.email);
    await prefs.setString('${_userKey}_displayName', user.displayName);
    await prefs.setString('${_userKey}_userId', user.userId);
    notifyListeners();
  }

  Future<void> logout() async {
    _user = null;
    _api.setToken(null);
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove(_tokenKey);
    await prefs.remove('${_userKey}_email');
    await prefs.remove('${_userKey}_displayName');
    await prefs.remove('${_userKey}_userId');
    notifyListeners();
  }
}

class ThemeService extends ChangeNotifier {
  static const _key = 'stockradar_theme';

  ThemeMode _mode = ThemeMode.dark;

  ThemeMode get mode => _mode;
  bool get isDark => _mode == ThemeMode.dark;

  Future<void> load() async {
    final prefs = await SharedPreferences.getInstance();
    final stored = prefs.getString(_key);
    if (stored == 'light') _mode = ThemeMode.light;
    if (stored == 'dark') _mode = ThemeMode.dark;
    notifyListeners();
  }

  Future<void> toggle() async {
    _mode = _mode == ThemeMode.dark ? ThemeMode.light : ThemeMode.dark;
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_key, _mode == ThemeMode.dark ? 'dark' : 'light');
    notifyListeners();
  }
}
