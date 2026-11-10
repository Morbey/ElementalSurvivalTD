# Elemental TD – Decisões de design (fonte de verdade)

> Só decisões fechadas e estado. Nada de discussão. Quem atualiza este ficheiro é o Cérebro Funcional, quando um tópico fecha.

## Plataformas e tecnologia
- Unity 6 (6000.6.8f1), URP, C#. 3D com câmara de cima, num estilo low-poly estilizado (referência: Bad North).
- WebGL e desktop ao mesmo nível. O primeiro marco é uma demo gratuita em WebGL no itch.io, para validar o jogo.
- Assets: packs gratuitos CC0 (Kenney, KayKit) no protótipo e na demo. A Synty POLYGON só se a demo tiver aceitação.

## Núcleo
- Um único sistema de modificadores sobre atributos. Alvos: torre, elemento, herói e global (ouro, XP, sorte, número de perks, inimigos por onda).
- Perks por subida de nível, através de XP: três opções, escolhe-se uma, e o jogo pausa durante a escolha.
- Aleatoriedade controlada nas perks: o jogador escolhe o elemento inicial, o sorteio tem pesos a favor dos elementos já obtidos, e nos primeiros níveis aparece sempre pelo menos uma opção de counter ao cenário.
- Sorte: 0 é sorteio puro. Mais sorte significa maior raridade, mais opções e mais elementos fortes contra o cenário.
- Ondas automáticas por tempo, com intervalo entre 5 e 20 s, sem pausa manual. O ecrã mostra sempre a próxima onda, com o inimigo e o elemento.
- Mapa: grelha aberta onde o jogador constrói um labirinto. Há pathfinding e é proibido bloquear o caminho. Os voadores vão em linha reta.
- Economia: ouro por abate e juros no início de cada onda, com um limite que sobe através de talentos.
- Heróis: dados mais habilidades reutilizáveis (tanque, feiticeiro, suporte). Lutam sozinhos e podem ser reposicionados.
- Talentos entre partidas: árvores por torre, por herói e de economia/sorte. São modificadores permanentes guardados no perfil.
- Inimigos: dados mais comportamentos (voar, regenerar, ficar invisível, dividir-se).
- Cenário: define um subconjunto de elementos inimigos, que se vê no menu e na pausa.
- Boss em 3D: fica para a fase 2 e não é essencial. Só avança depois de a demo validar o jogo.

## Sistema elemental
- Elementos base (6): **Fire, Water, Earth, Air, Light, Shadow**.
- Ciclo principal: Water > Fire > Air > Earth > Water. Light e Shadow são fortes um contra o outro.
  - Dano **x2** contra o elemento fraco e **x0,25** contra o forte.
- Resistências secundárias (dano **x0,75**): Earth resiste a Fire, Fire a Shadow, Air a Light e Shadow a Earth.
- A imunidade ao próprio elemento é absoluta e nada a quebra, nem o Cursed.
- Híbridas: há 15 combinações de dois elementos, e cada elemento base entra em cinco. Algumas híbridas de três elementos aparecem no fim da partida, como raras.
- Primeiro lote de híbridas, com cada elemento base a aparecer uma vez:
  - **Nature** = Earth + Water
  - **Lightning** = Air + Light
  - **Hellfire** = Fire + Shadow
  - O **Poison** (Water + Shadow) fica para o lote seguinte.

## Estados
- **Burning** (Fire): dano constante e de curta duração.
- **Cursed** (Hellfire): enquanto o alvo arde, as resistências dele ficam reduzidas a metade (25% passa a cerca de 62% e 75% a cerca de 87%).
- **Poisoned** (Poison): começa com pouco dano e vai aumentando, dura mais do que o Burning e no fim faz muito dano. É forte contra bosses e inimigos lentos.
- **Slowed** (Nature): reduz a velocidade e tem sinergia com o Poison.
- **Disoriented** (Lightning): o inimigo para e anda às voltas sobre si mesmo. Depois do efeito fica imune durante algum tempo, e nos bosses a duração é menor.

## Convenções
- Os nomes do jogo (elementos, estados, torres, perks, heróis, inimigos) ficam sempre em inglês.

## Próximos passos
- [ ] Definir o âmbito do protótipo mínimo (grelha, pathfinding, uma onda, duas torres, modificadores).
- [ ] Primeiro ADR do tech-lead: estrutura de pastas, asmdefs e camadas.
- [ ] Definir as 12 híbridas restantes e os respetivos estados.
- [ ] Lista de perks, heróis iniciais e cenários.
- [ ] Valores iniciais de balanceamento: resistências, durações e curva de XP.
