import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/api/api_client.dart';
import '../../core/services/app_services.dart';
import '../../widgets/glass_card.dart';
import '../../widgets/juice_logo.dart';
import '../../widgets/wave_background.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _email = TextEditingController();
  final _password = TextEditingController();
  final _displayName = TextEditingController();
  var _mode = _AuthMode.login;
  var _loading = false;
  String? _error;

  @override
  void dispose() {
    _email.dispose();
    _password.dispose();
    _displayName.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final auth = context.read<AuthService>();
      if (_mode == _AuthMode.login) {
        await auth.login(_email.text.trim(), _password.text);
      } else {
        final name = _displayName.text.trim().isEmpty
            ? _email.text.split('@').first
            : _displayName.text.trim();
        await auth.register(_email.text.trim(), _password.text, name);
      }
      if (mounted) context.go('/');
    } on ApiException catch (e) {
      setState(() => _error = e.message);
    } catch (e) {
      setState(() => _error = 'Đăng nhập thất bại');
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final themeService = context.watch<ThemeService>();
    final bottomInset = MediaQuery.viewInsetsOf(context).bottom;

    return Scaffold(
      resizeToAvoidBottomInset: true,
      body: WaveBackground(
        child: SafeArea(
          child: Column(
            children: [
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    TextButton(onPressed: () => context.go('/'), child: const Text('← Trang chủ')),
                    IconButton(
                      onPressed: themeService.toggle,
                      icon: Icon(themeService.isDark ? Icons.light_mode_outlined : Icons.dark_mode_outlined),
                    ),
                  ],
                ),
              ),
              Expanded(
                child: SingleChildScrollView(
                  padding: EdgeInsets.fromLTRB(16, 0, 16, 16 + bottomInset),
                  keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
                  child: Align(
                    alignment: Alignment.topCenter,
                    child: ConstrainedBox(
                      constraints: const BoxConstraints(maxWidth: 420),
                      child: GlassCard(
                        padding: const EdgeInsets.fromLTRB(24, 24, 24, 24),
                        child: Column(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            const SizedBox(
                              width: double.infinity,
                              child: JuiceLogo(variant: JuiceLogoVariant.full, size: JuiceLogoSize.xl),
                            ),
                            const SizedBox(height: 16),
                            Text(
                              _mode == _AuthMode.login ? 'Welcome back' : 'Tạo tài khoản',
                              style: Theme.of(context).textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.w600),
                            ),
                            const SizedBox(height: 8),
                            Row(
                              mainAxisAlignment: MainAxisAlignment.center,
                              children: [
                                Icon(Icons.shield_outlined, size: 16, color: scheme.primary),
                                const SizedBox(width: 6),
                                Flexible(
                                  child: Text(
                                    'Đăng nhập để quản lý watchlist cá nhân',
                                    style: TextStyle(fontSize: 13, color: scheme.onSurfaceVariant),
                                    textAlign: TextAlign.center,
                                  ),
                                ),
                              ],
                            ),
                            const SizedBox(height: 16),
                            _tabBar(scheme),
                            const SizedBox(height: 16),
                            TextField(
                              controller: _email,
                              keyboardType: TextInputType.emailAddress,
                              textInputAction: TextInputAction.next,
                              decoration: const InputDecoration(labelText: 'EMAIL HOẶC TÊN ĐĂNG NHẬP'),
                            ),
                            const SizedBox(height: 12),
                            if (_mode == _AuthMode.register) ...[
                              TextField(
                                controller: _displayName,
                                textInputAction: TextInputAction.next,
                                decoration: const InputDecoration(labelText: 'TÊN HIỂN THỊ'),
                              ),
                              const SizedBox(height: 12),
                            ],
                            TextField(
                              controller: _password,
                              obscureText: true,
                              textInputAction: TextInputAction.done,
                              decoration: const InputDecoration(labelText: 'MẬT KHẨU'),
                              onSubmitted: (_) => _submit(),
                            ),
                            if (_error != null) ...[
                              const SizedBox(height: 12),
                              Text(_error!, style: TextStyle(color: scheme.error, fontSize: 13)),
                            ],
                            const SizedBox(height: 16),
                            SizedBox(
                              width: double.infinity,
                              child: FilledButton(
                                onPressed: _loading ? null : _submit,
                                child: _loading
                                    ? const SizedBox(
                                        height: 20,
                                        width: 20,
                                        child: CircularProgressIndicator(strokeWidth: 2),
                                      )
                                    : Text(_mode == _AuthMode.login ? 'Sign In →' : 'Đăng ký →'),
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                  ),
                ),
              ),
              Padding(
                padding: const EdgeInsets.only(bottom: 10),
                child: Text(
                  '© 2026 JUICE · Smart Money Monitor',
                  style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _tabBar(ColorScheme scheme) {
    return Container(
      padding: const EdgeInsets.all(4),
      decoration: BoxDecoration(
        color: scheme.surface.withValues(alpha: 0.5),
        borderRadius: BorderRadius.circular(12),
      ),
      child: Row(
        children: [
          _tab('Đăng nhập', _AuthMode.login, scheme),
          _tab('Đăng ký', _AuthMode.register, scheme),
        ],
      ),
    );
  }

  Expanded _tab(String label, _AuthMode mode, ColorScheme scheme) {
    final active = _mode == mode;
    return Expanded(
      child: GestureDetector(
        onTap: () => setState(() => _mode = mode),
        child: Container(
          padding: const EdgeInsets.symmetric(vertical: 10),
          decoration: BoxDecoration(
            color: active ? Theme.of(context).cardColor : Colors.transparent,
            borderRadius: BorderRadius.circular(10),
            boxShadow: active ? [BoxShadow(color: Colors.black.withValues(alpha: 0.06), blurRadius: 4)] : null,
          ),
          alignment: Alignment.center,
          child: Text(
            label,
            style: TextStyle(
              fontWeight: FontWeight.w600,
              fontSize: 13,
              color: active ? scheme.primary : scheme.onSurfaceVariant,
            ),
          ),
        ),
      ),
    );
  }
}

enum _AuthMode { login, register }
