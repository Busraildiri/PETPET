import { Ionicons } from '@expo/vector-icons';
import { StatusBar } from 'expo-status-bar';
import { useCallback, useEffect, useRef, useState } from 'react';
import { ActivityIndicator, Image, KeyboardAvoidingView, Platform, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { getPatiMatchMessages, mediaUrl, sendPatiMatchMessage, type PatiMatchCandidate, type PatiMatchMessage, type PatiMatchMyPet } from '../api';
import { colors, createThemedStyles, getThemeMode, shadow } from '../theme';

type Props = {
  token: string;
  sourcePet: PatiMatchMyPet;
  match: PatiMatchCandidate;
  onBack: () => void;
};

export function PatiMatchChatScreen({ token, sourcePet, match, onBack }: Props) {
  const [messages, setMessages] = useState<PatiMatchMessage[]>([]);
  const [draft, setDraft] = useState('');
  const [loading, setLoading] = useState(true);
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const scrollRef = useRef<ScrollView>(null);

  const load = useCallback(async (quiet = false) => {
    const controller = new AbortController();
    if (!quiet) setLoading(true);
    try {
      const result = await getPatiMatchMessages(token, sourcePet.id, match.petId, controller.signal);
      setMessages(result);
      setError(null);
    } catch (reason) {
      if (!quiet) setError(reason instanceof Error ? reason.message : 'Sohbet açılamadı.');
    } finally {
      if (!quiet) setLoading(false);
    }
    return () => controller.abort();
  }, [match.petId, sourcePet.id, token]);

  useEffect(() => { void load(); }, [load]);
  useEffect(() => {
    const timer = setInterval(() => void load(true), 5000);
    return () => clearInterval(timer);
  }, [load]);
  useEffect(() => {
    if (messages.length) setTimeout(() => scrollRef.current?.scrollToEnd({ animated: true }), 80);
  }, [messages.length]);

  const send = async () => {
    const body = draft.trim();
    if (!body || sending) return;
    setSending(true);
    try {
      const saved = await sendPatiMatchMessage(token, sourcePet.id, match.petId, body);
      setMessages(current => [...current, saved]);
      setDraft('');
      setError(null);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Mesaj gönderilemedi.');
    } finally {
      setSending(false);
    }
  };

  return <KeyboardAvoidingView style={styles.screen} behavior={Platform.OS === 'ios' ? 'padding' : undefined} keyboardVerticalOffset={8}>
    <StatusBar style={getThemeMode() === 'dark' ? 'light' : 'dark'} />
    <View style={styles.header}>
      <Pressable onPress={onBack} accessibilityRole="button" accessibilityLabel="Eşleşmelere dön" style={styles.backButton}>
        <Ionicons name="arrow-back" size={23} color={colors.primary} />
      </Pressable>
      <Image source={{ uri: mediaUrl(match.profileImage) }} style={styles.avatar} />
      <View style={styles.headerCopy}>
        <Text style={styles.title}>{match.name}</Text>
        <Text style={styles.subtitle}>{sourcePet.name} ile PatiMatch</Text>
      </View>
      <Ionicons name="shield-checkmark-outline" size={23} color="#4E7458" />
    </View>

    <View style={styles.safetyBanner}>
      <Ionicons name="information-circle-outline" size={19} color="#4E7458" />
      <Text style={styles.safetyText}>Telefon, açık adres ve ödeme bilgilerini paylaşma. İlk görüşmeyi halka açık bir yerde yap.</Text>
    </View>

    <ScrollView ref={scrollRef} style={styles.messages} contentContainerStyle={styles.messageContent} keyboardShouldPersistTaps="handled">
      {loading ? <ActivityIndicator color={colors.primary} size="large" /> : null}
      {!loading && !messages.length && !error ? <View style={styles.empty}>
        <Ionicons name="chatbubbles-outline" size={40} color={colors.primary} />
        <Text style={styles.emptyTitle}>İlk mesajı sen gönder</Text>
        <Text style={styles.emptyText}>{sourcePet.name} ve {match.name} için güvenli bir tanışma başlatabilirsin.</Text>
      </View> : null}
      {messages.map(message => <View key={message.id} style={[styles.messageRow, message.isMine && styles.messageRowMine]}>
        <View style={[styles.bubble, message.isMine ? styles.bubbleMine : styles.bubbleOther]}>
          {!message.isMine ? <Text style={styles.sender}>{message.username}</Text> : null}
          <Text style={[styles.body, message.isMine && styles.bodyMine]}>{message.body}</Text>
          <Text style={[styles.time, message.isMine && styles.timeMine]}>{formatTime(message.createdAt)}</Text>
        </View>
      </View>)}
    </ScrollView>

    {error ? <Text style={styles.error}>{error}</Text> : null}
    <View style={styles.composer}>
      <TextInput
        value={draft}
        onChangeText={setDraft}
        placeholder="Mesajını yaz…"
        placeholderTextColor={colors.muted}
        style={styles.input}
        multiline
        maxLength={1000}
        accessibilityLabel="Mesaj"
      />
      <Pressable disabled={!draft.trim() || sending} onPress={() => void send()} accessibilityRole="button" accessibilityLabel="Mesajı gönder" style={[styles.sendButton, (!draft.trim() || sending) && styles.disabled]}>
        {sending ? <ActivityIndicator size="small" color={colors.white} /> : <Ionicons name="send" size={20} color={colors.white} />}
      </Pressable>
    </View>
  </KeyboardAvoidingView>;
}

function formatTime(value: string) {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '' : date.toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' });
}

const styles = createThemedStyles(() => ({
  screen:{flex:1,backgroundColor:colors.background,paddingTop:Platform.OS==='ios'?54:24},
  header:{minHeight:66,flexDirection:'row',alignItems:'center',gap:11,paddingHorizontal:18,borderBottomWidth:1,borderBottomColor:colors.border,backgroundColor:colors.card},
  backButton:{width:38,height:38,borderRadius:19,alignItems:'center',justifyContent:'center',backgroundColor:colors.lilacSoft},
  avatar:{width:43,height:43,borderRadius:15,backgroundColor:colors.lilacSoft},headerCopy:{flex:1},
  title:{color:colors.text,fontSize:16,fontWeight:'900'},subtitle:{color:colors.muted,fontSize:9,marginTop:2},
  safetyBanner:{flexDirection:'row',alignItems:'flex-start',gap:8,backgroundColor:colors.sageSoft,paddingHorizontal:18,paddingVertical:11},
  safetyText:{flex:1,color:'#4E7458',fontSize:9,lineHeight:14},messages:{flex:1},messageContent:{padding:18,paddingBottom:25},
  empty:{alignItems:'center',paddingTop:70,paddingHorizontal:28},emptyTitle:{color:colors.text,fontSize:18,fontWeight:'900',marginTop:12},emptyText:{color:colors.muted,fontSize:11,lineHeight:17,textAlign:'center',marginTop:6},
  messageRow:{alignItems:'flex-start',marginBottom:9},messageRowMine:{alignItems:'flex-end'},bubble:{maxWidth:'82%',borderRadius:18,paddingHorizontal:13,paddingVertical:10,...shadow},
  bubbleOther:{backgroundColor:colors.card,borderWidth:1,borderColor:colors.border,borderBottomLeftRadius:5},bubbleMine:{backgroundColor:colors.primary,borderBottomRightRadius:5},
  sender:{color:colors.primary,fontSize:8,fontWeight:'900',marginBottom:3},body:{color:colors.text,fontSize:12,lineHeight:18},bodyMine:{color:colors.white},
  time:{color:colors.muted,fontSize:7,textAlign:'right',marginTop:4},timeMine:{color:'#E8DAE5'},error:{color:colors.danger,fontSize:9,textAlign:'center',paddingHorizontal:18,paddingVertical:5},
  composer:{flexDirection:'row',alignItems:'flex-end',gap:9,paddingHorizontal:14,paddingTop:10,paddingBottom:Platform.OS==='ios'?34:14,backgroundColor:colors.card,borderTopWidth:1,borderTopColor:colors.border},
  input:{flex:1,maxHeight:110,minHeight:45,borderRadius:18,borderWidth:1,borderColor:colors.border,backgroundColor:colors.background,paddingHorizontal:14,paddingVertical:12,color:colors.text,fontSize:12},
  sendButton:{width:45,height:45,borderRadius:17,alignItems:'center',justifyContent:'center',backgroundColor:colors.primary},disabled:{opacity:.4},
}));
